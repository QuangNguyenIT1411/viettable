@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0cleanup-test-data.ps1" %*
