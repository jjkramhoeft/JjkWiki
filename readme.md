# JJK Private Wikipedia

My (Jens Jakob Kramhøft) experiment with a personal encyclopedia with search powered by LLM embeddings

Wikipedia dump files are not included in GitHub, I used the full english monthly xml dump files

The monthly xml dump of filewikipedia are trimmed, cleaned and indexed using vector embeddings from an LLM.

## Performence

After some light optimazition (apx. 10 hours.) the cleaning is down to 6 hours and indexing is another 6 hours. Then all can be loaded in memory in 12 seconds and a search for the 10 best matching wiki posts takes apx. 400 ms. 8.5 millions wikipages was used (all redirects was skipped)

## Effort

Most of the effort went in to cleaning the internal wiki page xml format to something that more resembles plain text, while keeping the cleaning fast. String handling had to be done with char arrays and string builder to a large extend.

## Layout Of Wikipidea Dump Files

### Monthly Dump

* Index file (byte:pageId:Title)
* Content file (page)
* Page (title:ns:id:revision)
** Revision (id:timestamp:text)

## JJK Wiki Model

### JJK Index (Memory and SQLite)

* Title: (pageId:title) (int:string) A subset of index file
* Age: (pageId:dayNumber:used) (int,int,bit) A micro subset of dumpfile
* Embedding: (embedding,pageId,type) (vector256,int,byte)

### JJK DB (SQLite)

* Text (pageId:text)

## JJK Solution Model

* Model
* Storage
* WorkerConsoleApp
* TestConsoleApp

### Creation Story Template

Solution folder JjkWiki

Terminal -> New Terminal: Use console in folder to create and add Model and Storage

`dotnet new sln`
`dotnet new classlib -o Model`
`dotnet sln add Model`
`dotnet new classlib -o Storage`
`dotnet sln add Storage`

Use console in folder to create references

`cd Storage`
`dotnet add Storage.csproj reference ../Model/Model.csproj`
`cd ..`

Use console in folder to create test console

`dotnet new console -o TestConsole`
`dotnet sln add TestConsole`
`dotnet add TestConsole/TestConsole.csproj reference Model/Model.csproj`
`dotnet add TestConsole/TestConsole.csproj reference Storage/Storage.csproj`

Use console to add NugetPackages for SQLite

`cd Storage`
`dotnet add package Microsoft.EntityFrameworkCore.Sqlite`
