::@echo off
:top
cls
c:
cd \setup\chatserver
move staging\*.*
call WPFChatServer.exe

:: If chat server is stopped, returns 0, otherwise 1
if %ERRORLEVEL%==1 goto top
