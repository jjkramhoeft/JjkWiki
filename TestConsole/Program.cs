using Storage;
using Model;
using System.Diagnostics;
using ICSharpCode.SharpZipLib.BZip2;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using SmartComponents.LocalEmbeddings;

LocalDb store = new("test-wiki");
store.InitStore();
var index = @"E:\WikiDump20241220\enwiki-20241220-pages-articles-multistream-index.txt";
var dump = @"E:\WikiDump20241220\enwiki-20241220-pages-articles-multistream.xml.bz2";

var action = Action.calcEmbeddings;

if (action == Action.insertTitles)
{
    Console.WriteLine("Insert Titles!");
    Stopwatch clock = new();
    var PageIndexStream = new FileStream(index, FileMode.Open);
    using var PageIndexStreamReader = new StreamReader(PageIndexStream);
    var line = string.Empty;
    List<Title> titles = [];
    clock.Start();
    while ((line = PageIndexStreamReader.ReadLine()) != null)
    {
        var pageIndexItem = PageIndexItem.Parse(line);
        titles.Add(new(pageIndexItem.PageId, pageIndexItem.PageTitle));
    }
    Console.WriteLine($"Reading all from Wiki Index {clock.Elapsed.TotalSeconds}(s)");
    clock.Restart();
    store.InsertTitles(titles);
    Console.WriteLine($"Writing all SQLite table {clock.Elapsed.TotalSeconds}(s)");
}
else if (action == Action.insertPageInfo)
{
    int maxReadAndParseSecs = 120000;
    Console.WriteLine($"Insert PageInfo! (max run seconds {maxReadAndParseSecs})");
    Stopwatch clockIndexReading = new();
    Stopwatch clockDumpReading = new();
    Stopwatch clockXmlParsing = new();
    Stopwatch clockWriting = new();
    Stopwatch clockTotal = new();
    clockTotal.Start();
    clockIndexReading.Start();
    var PageIndexStream = new FileStream(index, FileMode.Open);
    using var PageIndexStreamReader = new StreamReader(PageIndexStream);
    var line = string.Empty;
    HashSet<long> blockStarts = [];
    while ((line = PageIndexStreamReader.ReadLine()) != null)
    {
        var pageIndexItem = PageIndexItem.Parse(line);
        blockStarts.Add(pageIndexItem.ByteStart);
    }
    clockIndexReading.Stop();
    Console.WriteLine($"Reading Index {clockIndexReading.Elapsed.Seconds}(s)");
    int blockCountTotal = blockStarts.Count;
    int blockCountCurrent = 0;
    List<PageInfo> pagesInfo = [];
    using var dataDumpStream = new FileStream(dump, FileMode.Open);
    foreach (var byteStart in blockStarts)
    {
        if (1000 * maxReadAndParseSecs < clockTotal.ElapsedMilliseconds)
            break;
        blockCountCurrent++;
        if (blockCountCurrent % 100 == 0)
            Console.WriteLine($"{blockCountCurrent} blocks out of {blockCountTotal}");
        clockDumpReading.Start();
        dataDumpStream.Seek(byteStart, SeekOrigin.Begin);
        using var decompressedStream = new MemoryStream();
        BZip2.Decompress(dataDumpStream, decompressedStream, false);
        var streamBytes = decompressedStream.ToArray();
        var streamText = "<xml>" + System.Text.Encoding.Default.GetString(streamBytes) + "</xml>";
        clockDumpReading.Stop();
        clockXmlParsing.Start();
        var xmlDoc = XDocument.Parse(streamText);
        var pageElements = xmlDoc?.Element("xml")?.Elements("page");
        if (pageElements is not null)
            foreach (var pageElement in pageElements)
            {
                var idTag = pageElement?.Elements("id")?.First()?.Value ?? "";
                int pageId = int.Parse(idTag);
                var name = pageElement?.Elements("title")?.First()?.Value ?? "";
                var dateTag = pageElement?.Elements("revision").Elements("timestamp").First().Value ?? "";
                var date = DateTime.Parse(dateTag);
                var dayNumber = DateOnly.FromDateTime(date).DayNumber;
                var wikiPageType = pageElement?.Elements("ns").First().Value ?? "";
                var text = pageElement?.Elements("revision").Elements("text").First().Value ?? "";
                bool isRedirect = text.StartsWith("#Redirect", StringComparison.InvariantCultureIgnoreCase);
                int used = 0;
                if (!isRedirect && (wikiPageType.Equals("0") || wikiPageType.Equals("4")))
                    used = 1;
                pagesInfo.Add(new(pageId, dayNumber, used));
            }
        clockXmlParsing.Stop();
    }
    clockWriting.Start();
    store.InsertPagesInfo(pagesInfo);
    clockWriting.Stop();
    Console.WriteLine($"Done {blockCountCurrent} out of {blockCountTotal} ({100 * blockCountCurrent / blockCountTotal}%)");
    Console.WriteLine($"Reading Zipped Dump {clockDumpReading.Elapsed.TotalSeconds}(s)");
    Console.WriteLine($"Parsing XML {clockXmlParsing.Elapsed.TotalSeconds}(s)");
    double mult = 1.0 * blockCountTotal / blockCountCurrent;
    var fullRunTime = mult / 1000.0 * (
        clockDumpReading.Elapsed.TotalMilliseconds +
        clockXmlParsing.Elapsed.TotalMilliseconds +
        clockWriting.Elapsed.TotalMilliseconds);
    Console.WriteLine($"Estimated full run timer {(int)fullRunTime}(s) ({(int)(fullRunTime / 60)} minutes) mult:{(int)mult}");
    Console.WriteLine($"Writing SQLite table {clockWriting.Elapsed.TotalSeconds}(s)");
}
else if (action == Action.cleanPages)
{
    Console.WriteLine("Cleaning Pages");

    Stopwatch clockReadDb = new();
    clockReadDb.Start();
    var pagesInfo = store.GetAllPagesInfo();
    clockReadDb.Stop();
    Console.WriteLine($"Got {pagesInfo.Count} pagesInfo in {(int)clockReadDb.Elapsed.TotalSeconds}(s)");

    Stopwatch clockReadWikiIndex = new();

    var PageIndexStream = new FileStream(index, FileMode.Open);
    using var PageIndexStreamReader = new StreamReader(PageIndexStream);
    var line = string.Empty;
    Dictionary<int, long> indices = [];
    clockReadWikiIndex.Start();
    while ((line = PageIndexStreamReader.ReadLine()) != null)
    {
        var pageIndexItem = PageIndexItem.Parse(line);
        indices.Add(pageIndexItem.PageId, pageIndexItem.ByteStart);
    }
    clockReadWikiIndex.Stop();
    PageIndexStream.Close();
    PageIndexStream.Dispose();
    Console.WriteLine($"Reading all from Wiki Index {(int)clockReadWikiIndex.Elapsed.TotalSeconds}(s)");

    Stopwatch clockJoin = new();
    clockJoin.Start();
    List<Page> result = [];
    foreach (var pageInfo in pagesInfo)
        if (pageInfo.Used == 1)
        {
            if (indices.TryGetValue(pageInfo.PageId, out long byteStart))
                result.Add(new(pageInfo.PageId, pageInfo.DayNumber, byteStart));
            else
            {
                throw new Exception($"Missing pageId ({pageInfo.PageId}) in title tables!");
            }
        }
    var sortedResult = result.OrderBy(r => r.ByteStart);
    Dictionary<long, List<PageInfo>> res = [];
    long prev = 0;
    List<PageInfo> currentList = [];
    foreach (var r in sortedResult)
    {
        if (prev != r.ByteStart)
        {
            if (0 < prev)
                res.Add(prev, currentList);
            currentList = [];
        }
        currentList.Add(new(r.PageId, r.DayNumber, 1));
        prev = r.ByteStart;
    }
    clockJoin.Stop();
    Console.WriteLine($"Joined {result.Count} together and sorted in {(int)clockJoin.Elapsed.TotalSeconds}(s)");

    //store.TruncateCleanedPages();
    //store.Vacuum();

    int count = 0;
    using var dataDumpStream = new FileStream(dump, FileMode.Open);
    List<CleanPage> chunckOfPages = [];

    Stopwatch clockAll = new();
    Stopwatch clockClean = new();
    clockAll.Start();
    foreach (var keyValuePair in res)
    {
        dataDumpStream.Seek(keyValuePair.Key, SeekOrigin.Begin);
        using var decompressedStream = new MemoryStream();
        BZip2.Decompress(dataDumpStream, decompressedStream, false);
        var streamBytes = decompressedStream.ToArray();
        var streamText = "<xml>" + Encoding.Default.GetString(streamBytes) + "</xml>";
        var xmlDoc = XDocument.Parse(streamText);
        foreach (var pageInfo in keyValuePair.Value)
        {
            var pageElement = xmlDoc?.Element("xml")?.Elements("page").Where(x => x.Element("id")?.Value == pageInfo.PageId.ToString()).FirstOrDefault();
            if (pageElement is not null)
            {
                var name = pageElement?.Elements("title")?.First()?.Value ?? "";
                var dateTag = pageElement?.Elements("revision").Elements("timestamp").First().Value ?? "";
                var date = DateTime.Parse(dateTag);
                var dayNumber = DateOnly.FromDateTime(date).DayNumber;
                //var wikiPageType = pageElement?.Elements("ns").First().Value ?? "";  // we should be at an 0 or 4
                var text = pageElement?.Elements("revision").Elements("text").First().Value ?? "";
                //bool isRedirect = text.StartsWith("#Redirect", StringComparison.InvariantCultureIgnoreCase);  // we should not be at a redirect
                clockClean.Start();
                var text2 = Clean(text);
                clockClean.Stop();
                chunckOfPages.Add(new(pageInfo.PageId, text2));
                count++;
                if (count % 1000 == 0)
                {
                    store.InsertCleanedPages(chunckOfPages);
                    chunckOfPages = [];
                    double progres = 100.0 * count / result.Count;
                    long secondsSpent = clockAll.ElapsedMilliseconds / 1000;
                    var cleanFrac = 100.0 * clockClean.ElapsedMilliseconds / clockAll.ElapsedMilliseconds;
                    var estMin = (secondsSpent / 60.0) * 100.0 / progres;
                    Console.WriteLine($"{count} ({(int)progres}%) Time spent on cleaning {(int)clockClean.Elapsed.TotalSeconds}s  Fraction of time spent cleaning {(int)cleanFrac}%  Expected total time {(int)estMin}(min)");
                }
            }
            else
            {
                throw new Exception("Missing page");
            }
        }
    }
}
else if (action == Action.testCleaning)
{
    List<string> testPages = TestData.TestPages;
    int count = 0;
    foreach (var p in testPages)
    {
        for (int i = 0; i < 1; i++)
        {
            count++;
            var cleanNew = Clean(p);
        }
    }
    Console.WriteLine($"{count} runs.");
}
else if (action == Action.calcEmbeddings)
{
    Stopwatch clock = new();
    Stopwatch clockInit = new();
    Stopwatch clockCalc = new();
    Stopwatch clockRead = new();
    Stopwatch clockWrite = new();
    clock.Start();
    clockInit.Start();
    Console.WriteLine("Calculate  Embedings!");
    using var embedder = new LocalEmbedder(modelName: "default");
    Console.WriteLine("Init.  LocalEmbedder!");
    var pageIds = store.GetAllUsedPageIds();
    Console.WriteLine($"Got {pageIds.Count} used pageIds");
    store.TruncatePageVectors();
    clockInit.Stop();
    int count = 0;
    foreach (var pageIdChunk in pageIds.Chunk(500))
    {
        List<PageVector> pageVectorList = [];
        //List<string> textList = [];
        List<PageText> pageList = [];
        foreach (var pageId in pageIdChunk)
        {
            clockRead.Start();
            string text = store.GetPageText(pageId);
            pageList.Add(new(pageId,text));
            clockRead.Stop();
            count++;
        }

        clockCalc.Start();      
        var embedRange = embedder.EmbedRange<PageText,EmbeddingI8>(pageList,p => p.Text);  //Persist via buffer - https://github.com/dotnet-smartcomponents/smartcomponents/blob/main/docs/local-embeddings.md   
        foreach(var (Item, Embedding) in embedRange)
            pageVectorList.Add(new(Item.PageId, Embedding.Buffer.ToArray()));    
        clockCalc.Stop();

        clockWrite.Start();
        store.InsertPageVectors(pageVectorList);
        clockWrite.Stop();
        
        double progres = count/pageIds.Count;
        double timePrPage = clock.ElapsedMilliseconds/count;
        double estTotalMinutes = timePrPage*pageIds.Count/60000.0;
        Console.WriteLine(
            $"Done {count}, Progres: {100*progres} %, "+
            $"Init: {(int)clockInit.Elapsed.TotalSeconds} s, Read: {(int)clockRead.Elapsed.TotalSeconds} s, Calc: {(int)clockCalc.Elapsed.TotalSeconds} s, Write: {(int)clockWrite.Elapsed.TotalSeconds} s, "+
            $"Estimated total minutes: {(int)estTotalMinutes}, Avg. sec. pr. page: {timePrPage} ms");
        if (25000 < count)
            break;
    }
    Console.WriteLine($"Done."); // at current perf it will take 18 hours for total eng wikipedia
    clock.Stop();
}
else if (action == Action.none)
{
    Console.WriteLine("No action!");
    DateOnly day = DateOnly.FromDayNumber(2);
    int dayNumber = day.DayNumber;
}
else
{
    throw new Exception("Action not set!");
}

// Remove unwanted charactes from wiki page text, to make it more like human written plain text
// (e.g. references, comments, links, tables, formatting ect.)
string Clean(string rawtext)
{
    string text = CutOff(rawtext);
    //clean per line
    var lines = text.Split('\n');
    StringBuilder newText = new();
    foreach (var currentLine in lines)
    {
        var line = RemoveFromTo(currentLine, "<!--", "-->");
        line = MyRegex().Replace(line, "");
        line = MyRegex1().Replace(line, "");
        line = RemoveFromTo(line, "{{", "}}");
        line = RemoveLinks(line, "[[", "]]", '|');// keep only first
        line = RemoveHeadingStyles(line);
        newText.Append(line);
    }
    text = RemoveTagsAndContent(newText.ToString(), "ref");
    text = RemoveTagsAndContent(text, "gallery");
    text = RemoveFromTo(text, "{{", "}}");
    text = RemoveFromTo(text, "{|", "|}");//Skip all tables   
    text = MultiTrim(text);
    return text;
}

// Only allow single '
// triple (and more) new lines => double new line
// nice bullit lists, \n*X => \n* X
// All multi space => single space
// All multi , => single ,
// Never space before ,
// No spaces in start or end of lines
// () =>
// &nbsp; => space
// &mdash; => -
// <br /> => \n
string MultiTrim(string text)
{
    var chars = text.ToCharArray();
    var length = chars.Length;

    Dictionary<string, char?> replaceDict = [];
    replaceDict.Add("()", null);
    replaceDict.Add("&nbsp;", ' ');
    replaceDict.Add("&mdash;", '-');
    replaceDict.Add("\r", null);
    replaceDict.Add("<br />", '\n');
    int replaceCount = replaceDict.Count;
    List<char[]> replaceChars = [];
    List<char?> replacementChar = [];
    int i = 0;
    foreach (var kvp in replaceDict)
    {
        replaceChars.Add(kvp.Key.ToCharArray());
        replacementChar.Add(kvp.Value);
        i++;
    }
    StringBuilder sb = new();

    bool regulatContentStarted = false;
    for (int current = 0; current < length; current++)
    {
        int left = length - current;

        if (!regulatContentStarted)
        {
            if (chars[current] == '\n' ||
                chars[current] == '\r' ||
                chars[current] == ' ')
                continue;
            else
                regulatContentStarted = true;
        }

        UnicodeCategory category = Char.GetUnicodeCategory(chars[current]);
        switch (category)
        {
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
                if ('\u03FF' < chars[current])
                    continue;
                break;
            default:
                break;
        }

        // All multi space => single space
        if (chars[current] == ' ' && 0 < left && chars[current + 1] == ' ')
            continue;

        // All multi , => single ,
        if (chars[current] == ',' && 0 < left && chars[current + 1] == ',')
            continue;

        // Never space before ,
        if (chars[current] == ' ' && 0 < left && chars[current + 1] == ',')
            continue;

        // No spaces in start of lines
        if (chars[current] == ' ' && 0 < current && chars[current - 1] == '\n')
            continue;
        // No spaces at end of lines
        if (chars[current] == ' ' && 0 < left && chars[current + 1] == '\n')
            continue;
        if (chars[current] == ' ' && 1 < left && chars[current + 1] == ' ' && chars[current + 1] == '\n')
        {
            current++;
            continue;
        }//ToDo remove more than 2 trailing spaces

        // Replacement
        bool replacement = false;
        char? newChar = null;
        for (i = 0; i < replaceCount; i++)
        {
            if (replacement == false &&
                chars[current] == replaceChars[i][0] &&
                replaceChars[i].Length - 1 < left)
            {
                bool matchesTheRest = true;
                for (int j = 1; j < replaceChars[i].Length; j++)
                {
                    if (replaceChars[i][j] != chars[current + j])
                        matchesTheRest = false;
                }
                if (matchesTheRest)
                {
                    current += replaceChars[i].Length - 1;
                    replacement = true;
                    newChar = replacementChar[i];
                }
            }
        }
        if (replacement)
        {
            if (newChar is not null)
                sb.Append(newChar);
            continue;
        }

        // Only allow single '
        int pingCount = 0;
        while (chars[current + pingCount] == '\'' &&
            pingCount + 1 < left &&
            pingCount < 5)
        {
            pingCount++;
        }
        if (1 < pingCount)
        {
            current += pingCount;
            continue;
        }

        // triple (and more) new lines => double new line
        int newLineCount = 0;
        while (chars[current + newLineCount] == '\n' &&
            newLineCount + 1 < left)
        {
            newLineCount++;
        }
        if (2 < newLineCount)
        {
            sb.Append('\n');
            current += newLineCount - 1;
            continue;
        }

        // Nice bullit list
        if (chars[current] == '\n' &&
            3 < left &&
            chars[current + 1] == '*' &&
            chars[current + 2] != ' ')
        {
            sb.Append("\n* ");
            current += 2;
            continue;
        }

        sb.Append(chars[current]);
    }

    string result = sb.ToString();
    return result.TrimEnd(' ', '\r', '\n');
}

string RemoveFromTo(string text, string from, string to)
{
    var chars = text.ToCharArray();
    var length = chars.Length;

    char[] tagStartChars = from.ToCharArray();
    char[] tagEndChars = to.ToCharArray();

    int matchingStartCount = 0;
    int matchingEndCount = 0;
    int startCount = 0;
    int endCount = 0;
    HashSet<int> handledCount = [];
    int currentStartIndex = -1;
    int endIndex = -1;
    List<(int, int)> removabels = [];

    for (int current = 0; current < length; current++)
    {
        if (chars[current] == tagStartChars[matchingStartCount])
        {
            matchingStartCount++;
            if (matchingStartCount == tagStartChars.Length)
            {
                startCount++;
                matchingStartCount = 0;
                if (currentStartIndex == -1)
                    currentStartIndex = current - tagStartChars.Length + 1;
            }
        }
        else if (0 < matchingStartCount)
        {
            matchingStartCount = 0;
        }

        if (endCount < startCount)
        {
            if (chars[current] == tagEndChars[matchingEndCount])
            {
                matchingEndCount++;
                if (matchingEndCount == tagEndChars.Length)
                {
                    endCount++;
                    matchingEndCount = 0;
                    endIndex = current;
                }
            }
            else if (0 < matchingEndCount)
            {
                matchingEndCount = 0;
            }
        }
        if (0 < startCount &&
            endCount == startCount &&
            !handledCount.Contains(startCount))
        {
            removabels.Add((currentStartIndex, endIndex));
            handledCount.Add(startCount);
            currentStartIndex = -1;
            endIndex = -1;
        }
    }

    if (removabels.Count == 0)
        return text;

    int count = 0;
    StringBuilder sb = new();

    for (int i = 0; i < length; i++)
    {
        if (count < removabels.Count &&
            removabels[count].Item1 <= i && i <= removabels[count].Item2)
        {
            //Remove
            if (i == removabels[count].Item2)
                count++;
        }
        else
            sb.Append(chars[i]);
    }
    string result = sb.ToString();
    return result;
}

string RemoveTagsAndContent(string text, string tagName)
{
    var chars = text.ToCharArray();
    var lowerChars = text.ToLower().ToArray();
    var length = chars.Length;

    char[] tagStartChars = new char[tagName.Length + 1];
    tagStartChars[0] = '<';
    int i = 1;
    foreach (char c in tagName.ToLower().ToCharArray())
    {
        tagStartChars[i] = c;
        i++;
    }

    char[] tagEndChars = new char[tagName.Length + 2];
    tagEndChars[0] = '<';
    tagEndChars[1] = '/';
    i = 2;
    foreach (char c in tagName.ToLower().ToCharArray())
    {
        tagEndChars[i] = c;
        i++;
    }

    char[] tagSelfCloseChars = ['/', '>'];

    int matchingStartCount = 0;
    int matchingEndCount = 0;
    int matchingSelfCloseCount = 0;
    int startCount = 0;
    int endCount = 0;
    int selfCloseCount = 0;
    bool inStart = false;
    HashSet<int> handledCount = [];
    int currentStartIndex = -1;
    List<(int, int)> removabels = [];

    for (int current = 0; current < length; current++)
    {
        if (lowerChars[current] == tagStartChars[matchingStartCount])
        {
            matchingStartCount++;
            if (matchingStartCount == tagStartChars.Length)
            {
                startCount++;
                matchingStartCount = 0;
                inStart = true;
                if (currentStartIndex == -1)
                    currentStartIndex = current - tagStartChars.Length + 1;
            }
        }
        else if (0 < matchingStartCount)
        {
            matchingStartCount = 0;
        }

        if (endCount + selfCloseCount < startCount)
        {
            if (inStart)
            {
                if (lowerChars[current] == '<')
                    inStart = false;
                if (lowerChars[current] == tagSelfCloseChars[matchingSelfCloseCount])
                {
                    matchingSelfCloseCount++;
                    if (matchingSelfCloseCount == tagSelfCloseChars.Length)
                    {
                        selfCloseCount++;
                        matchingSelfCloseCount = 0;
                        inStart = false;
                    }
                }
                else if (0 < matchingSelfCloseCount)
                {
                    matchingSelfCloseCount = 0;
                }
            }
            if (lowerChars[current] == tagEndChars[matchingEndCount])
            {
                matchingEndCount++;
                if (matchingEndCount == tagEndChars.Length)
                {
                    endCount++;
                    matchingEndCount = 0;
                }
            }
            else if (0 < matchingEndCount)
            {
                matchingEndCount = 0;
            }
        }
        if (0 < startCount &&
            endCount + selfCloseCount == startCount &&
            !handledCount.Contains(startCount))
        {
            if (lowerChars[current] == '>')
            {
                removabels.Add((currentStartIndex, current));
                handledCount.Add(startCount);
                currentStartIndex = -1;
            }
        }
    }

    if (removabels.Count == 0)
        return text;

    int count = 0;
    StringBuilder sb = new();

    for (i = 0; i < length; i++)
    {
        if (count < removabels.Count &&
            removabels[count].Item1 <= i && i <= removabels[count].Item2)
        {
            //Remove
            if (i == removabels[count].Item2)
                count++;
        }
        else
            sb.Append(chars[i]);
    }
    string result = sb.ToString();
    return result;
}

string CutOff(string text)
{
    string[] candidates = [
        "{{reflist",
        "==seealso==",
        "==notes==",
        "==references=="];
    int candidatesCount = candidates.Length;

    List<char[]> candidatesChars = [];
    int[] matchingCandidatesCount = new int[candidatesCount];
    int[] foundCandidatesFirstIndex = new int[candidatesCount];
    int i = 0;
    foreach (var candidate in candidates)
    {
        char[] candidateChars = candidate.ToCharArray();
        candidatesChars.Add(candidateChars);
        matchingCandidatesCount[i] = 0;
        foundCandidatesFirstIndex[i] = -1;
        i++;
    }

    int cutOffIndex = -1;
    var lowerChars = text.ToLower().ToArray();
    int length = lowerChars.Length;

    for (int current = 0; current < length; current++)
    {
        for (i = 0; i < candidatesCount; i++)
        {
            if (lowerChars[current] == ' ')
            {
                //ignoring spaces in match for cut off
            }
            else if (lowerChars[current] == candidatesChars[i][matchingCandidatesCount[i]])
            {
                matchingCandidatesCount[i]++;
                if (candidatesChars[i].Length == matchingCandidatesCount[i])
                {
                    if (foundCandidatesFirstIndex[i] == -1)
                        foundCandidatesFirstIndex[i] = current - candidatesChars[i].Length;
                    matchingCandidatesCount[i] = 0;
                }
            }
            else if (0 < matchingCandidatesCount[i])
            {
                matchingCandidatesCount[i] = 0;
            }
        }
    }

    for (i = 0; i < candidatesCount; i++)
    {
        if (-1 < foundCandidatesFirstIndex[i])
        {
            if (cutOffIndex == -1 || foundCandidatesFirstIndex[i] < cutOffIndex)
                cutOffIndex = foundCandidatesFirstIndex[i];
        }
    }
    if (cutOffIndex == -1)
        return text;// Did not find a macthing cut off
    else
    {
        var chars = text.ToArray();
        StringBuilder sb = new();
        for (i = 0; i < cutOffIndex; i++)
        {
            sb.Append(chars[i]);
        }
        return sb.ToString();
    }
}

string RemoveHeadingStyles(string text)
{
    var lines = text.Split('\n');
    StringBuilder result = new();
    foreach (var line in lines)
    {
        var trimed = line.Trim();
        if (trimed.StartsWith('=') && trimed.EndsWith('='))
            result.Append(trimed.Replace("=", "").Trim() + '\n');
        else
            result.Append(trimed + '\n');
    }
    return result.ToString();
}

string RemoveLinks(string text, string start, string end, char sep)
{
    var chars = text.ToCharArray();
    var length = chars.Length;

    char[] tagStartChars = start.ToCharArray();
    char[] tagEndChars = end.ToCharArray();

    int matchingStartCount = 0;
    int matchingEndCount = 0;
    int startCount = 0;
    int endCount = 0;
    HashSet<int> handledCount = [];
    List<int> seps = [];
    int currentStartIndex = -1;
    int endIndex = -1;
    List<(int, int)> removabels = [];

    for (int current = 0; current < length; current++)
    {
        if (chars[current] == sep && endCount < startCount)
        {
            seps.Add(current);
        }

        if (chars[current] == tagStartChars[matchingStartCount])
        {
            matchingStartCount++;
            if (matchingStartCount == tagStartChars.Length)
            {
                startCount++;
                matchingStartCount = 0;
                if (currentStartIndex == -1)
                    currentStartIndex = current - tagStartChars.Length + 1;
            }
        }
        else if (0 < matchingStartCount)
        {
            matchingStartCount = 0;
        }

        if (endCount < startCount)
        {
            if (chars[current] == tagEndChars[matchingEndCount])
            {
                matchingEndCount++;
                if (matchingEndCount == tagEndChars.Length)
                {
                    endCount++;
                    matchingEndCount = 0;
                    endIndex = current;
                }
            }
            else if (0 < matchingEndCount)
            {
                matchingEndCount = 0;
            }
        }
        if (0 < startCount &&
            endCount == startCount &&
            !handledCount.Contains(startCount))
        {
            removabels.Add((currentStartIndex, endIndex));
            handledCount.Add(startCount);
            currentStartIndex = -1;
            endIndex = -1;
        }
    }

    if (removabels.Count == 0)
        return text;

    int count = 0;
    StringBuilder sb = new();
    for (int i = 0; i < length; i++)
    {
        if (count < removabels.Count &&
            removabels[count].Item1 <= i && i <= removabels[count].Item2)
        {
            //find sep
            int fittingSep = -1;
            foreach (var aSep in seps)
            {
                if (removabels[count].Item1 + 2 < aSep && aSep < removabels[count].Item2)
                    fittingSep = aSep;
                break;
            }
            if (fittingSep == -1)
            {
                //found no sep
                fittingSep = removabels[count].Item2 - 1;
            }
            //link to keep
            int linkToKeepStart = removabels[count].Item1 + 2;
            int linkToKeepEnd = fittingSep - 1;
            if (linkToKeepEnd < linkToKeepStart)
            {
                //skip
            }
            else
            {
                for (int l = linkToKeepStart; l <= linkToKeepEnd; l++)
                {
                    sb.Append(chars[l]);
                }
            }
            //jump to end and continue
            i = removabels[count].Item2;
            count++;
            continue;
        }
        else
            sb.Append(chars[i]);
    }
    string result = sb.ToString();
    return result;
}

class Page(int pageId, int dayNumber, long byteStart)
{
    public int PageId { get; set; } = pageId;
    public int DayNumber { get; set; } = dayNumber;
    public long ByteStart { get; set; } = byteStart;
}

class PageText(int pageId, string text)
{
    public int PageId { get; set; } = pageId;
    public string Text { get; set; } = text;
}

enum Action
{
    none,
    cleanPages,
    testCleaning,
    insertTitles,
    insertPageInfo,
    calcEmbeddings
}

partial class Program
{
    [GeneratedRegex(@"\({{[respell|IPA].+}}\)")]
    private static partial Regex MyRegex();
}

partial class Program
{
    [GeneratedRegex(@"'{2,}")]
    private static partial Regex MyRegex1();
}