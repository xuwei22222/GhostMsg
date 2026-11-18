csc /t:library /out:UIServer.dll UIServer.cs
csc /t:winexe /r:UIServer.dll mainfrom.cs & pause