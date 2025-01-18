# JJK Private Wikipedia

## Layout Of Wikipidea Dump Files

### Monthly Dump

* Index file (byte:pageId:Title)
* Content file (page)
* Page (title:ns:id:revision)
** Revision (id:timestamp:text)

## JJK Wiki Model

### JJK Index (Memory and SQLite)

* Title: (pageId:title) (int:string) A subset of index file
* Age: (pageId:date:used) (int,int,bit) A micro subset of dumpfile
* Embedding: (embedding,pageId,type) (vector256,int,byte)

### JJK BD (SQLite)

* Text (pageId:text)

## JJK Solution Model

* Model
* Storage
* WorkerConsoleApp
* TestConsoleApp

### Creation Story Template

Solution folder JjkWiki

Terminal -> New Terminal
Use console in folder to create and add Model and Storage

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
