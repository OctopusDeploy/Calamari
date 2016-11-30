@echo off


SET project_directory=%1
SET compile_runtimeoutputdir=%2
SET compile_Configuration=%3

echo "------ Copying Extenstions: Start ------"


SET extensions_directory=%compile_runtimeoutputdir%\Fixtures\Extensions
echo Copying Extensions To %extensions_directory%

rmdir /Q /S %extensions_directory%
for %%x in (
        Calamari.Extensibility.RunScript
		Calamari.Extensibility.IIS
       ) do (
			xcopy %project_directory%\..\%%x\bin\%compile_Configuration% %extensions_directory%\%%x\ /E
       )

echo "------ Copying Extenstions: End ------"
