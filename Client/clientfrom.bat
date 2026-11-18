csc /t:library /out:UIClient.dll editini.cs UIClient.cs
csc /t:winexe /r:UIClient.dll clientfrom.cs
copy /Y "./12.ico" "./build/12.ico"
copy /Y "./clientfrom.exe" "./build/"
copy /Y "./config.ini" "./build/"
copy /Y "./UIClient.dll" "./build/"
pause