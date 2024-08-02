@echo off

pwsh.exe -ExecutionPolicy Bypass -NoProfile -NonInteractive -File "%~dp0\ask_pass.ps1" %*
