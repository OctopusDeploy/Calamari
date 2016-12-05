@echo off


SET project_directory=%1
SET compile_runtimeoutputdir=%2
SET compile_Configuration=%3

echo "------ Copying Extenstions: Start ------"


SET extensions_directory=%compile_runtimeoutputdir%\Fixtures\Extensions
echo Copying Extensions To %extensions_directory%

rmdir /Q /S %extensions_directory%
for %%x in (
        Calamari.Extensibility.FakeFeatures
       ) do (
			echo %project_directory%\..\%%x\bin\%compile_Configuration% to %extensions_directory%\%%x\0.0.0
			xcopy %project_directory%\..\%%x\bin\%compile_Configuration% %extensions_directory%\%%x\0.0.0\ /E
       )

echo "------ Copying Extenstions: End ------"
