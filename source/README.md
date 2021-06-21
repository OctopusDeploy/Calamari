Calamari
========

How to update ScriptCS
----------------------

As of now there is no nuget package that contains ScriptSC executable. Hopefully this will change in the future (https://github.com/scriptcs/scriptcs/issues/1061). So for now we can use the nuget package that is available on Chocolatey (https://chocolatey.org/packages/scriptcs). 

1. Go to https://chocolatey.org/packages/scriptcs
2. Click Download to get the package
3. Upload it to https://www.myget.org/feed/Packages/octopus-dependencies (login to MyGet and it provides a nice UI for that)
4. Update Calamari to the version you just uploaded