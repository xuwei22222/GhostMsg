@echo off
set libmsg="Libファイル(dllファイル)を入力してください："
set wfmsg="WinFrom作成したソースファイル(csファイル)を入力してください(単件：xxx.cs  複数件：xxx.cs yyy.cs)："
set exemsg="Exe作成したソースファイル(csファイル)を入力してください(単件：xxx.cs  複数件：xxx.cs yyy.cs)："

echo "下記引数を選択してください："
echo "1 コンパイルLibファイル"
echo "2 コンパイルWinFromファイル"
echo "3 コンパイルExeファイル"
echo "4 コンパイルLib + WinFromファイル"
echo "5 コンパイルLib + Exeファイル"
echo "6 終了"
echo "選択した後にEnterキーを入力してください："
set /p operate=
if %operate%==1 goto lib
if %operate%==2 goto win
if %operate%==3 goto exe
if %operate%==4 goto libwin
if %operate%==5 goto libexe
if %operate%==6 goto endps

:lib
echo %libmsg%
set /p slib=
echo "ソースファイル(csファイル)を入力してください(単件：xxx.cs  複数件：xxx.cs yyy.cs)："
set /p scs=
csc /t:library /out:%slib%.dll %scs% & pause
goto endps

:win
echo %wfmsg%
set /p scs=
csc /t:winexe %scs% & pause
goto endps

:exe
echo %exemsg%
set /p scs=
csc /t:exe %scs% & pause
goto endps

:libwin
echo %libmsg%
set /p slib=
echo %wfmsg%
set /p scs=
csc /t:winexe /r:%slib%.dll %scs% & pause
goto endps

:libexe
echo %libmsg%
set /p slib=
echo %exemsg%
set /p scs=
csc /t:exe /r:%slib%.dll %scs% & pause
goto endps

:endps
exit