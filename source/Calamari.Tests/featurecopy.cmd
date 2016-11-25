@echo off

SET project_directory=%1
SET compile_outputdir=%2
SET compile_Configuration=%3
SET compile_targetframework=%4
SET compile_fulltargetframework=%5
SET compile_runtimeoutputdir=%6
SET compile_runtimeidentifier=%7

echo "------ Copying Extenstions: Start ------"
REM echo %project_directory%
REM echo %compile_outputdir%
REM echo %compile_Configuration%
REM echo %compile_targetframework%
REM echo %compile_fulltargetframework%
REM echo %compile_runtimeoutputdir%
REM echo %compile_runtimeidentifier%

SET extensions_directory=%compile_outputdir%\Calamari.Extensions
rmdir /Q /S %extensions_directory%

for %%x in (
        Calamari.Extensibility.RunScript
		Calamari.Extensibility.IIS
       ) do (
         xcopy %project_directory%\..\%%x\bin\%compile_Configuration% %extensions_directory%\%%x\ /E
       )
REM xcopy %project_directory%\..\Calamari.Extensibility.IIS\bin\%compile_Configuration% %extensions_directory%\Calamari.Extensibility.IIS\ /E

echo "------ Copying Extenstions: End ------"
