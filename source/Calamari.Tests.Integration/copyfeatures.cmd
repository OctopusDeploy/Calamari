
#@echo off
REM SET project_directory=%1
REM SET compile_runtimeoutputdir=%2
REM SET compile_targetframework=%3
REM SET compile_configuration=%4

REM del /f /s /q %compile_runtimeoutputdir%\Extensions
REM rmdir /s /q %compile_runtimeoutputdir%\Extensions

REM mkdir %compile_runtimeoutputdir%\Extensions\Extensions.Calamari.Extensibility.RunScript
REM #xcopy %runscript% \Extensions\Calamari.Extensibility.RunScript /Y
REM echo  %project_directory%
REM echo  %compile_runtimeoutputdir%
REM echo  %compile_targetframework%
REM echo  %compile_configuration%