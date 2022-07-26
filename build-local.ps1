Write-Host "
##################################################################################################
#                                                                                                #
#  This is a helper script for running local Calamari builds, it's going to do the following:    #
#    * Append a timestamp to the NuGet package versions to give you a unique version number      #
#      without needing to commit your changes locally                                            #
#    * Set the msbuild verbosity to minimal to reduce noise                                      #
#    * Create NuGet packages for the various runtimes in parallel                                #
#    * Skip creating .Tests NuGet packages                                                       #
#    * Set the CalamariVersion property in Octopus.Server.csproj                                 #
#                                                                                                # 
#  This script is intended to only be run locally and not in CI.                                 #
#                                                                                                #
#  If something unexpected is happening in your build or Calamari changes you may want to run    #
#  the full build by running ./build.ps1 and check again as something in the optimizations here  #
#  might have caused an issue.                                                                   #
#                                                                                                #
##################################################################################################
" -ForegroundColor Cyan

./build.ps1 -BuildVerbosity Minimal -Verbosity Normal -PackInParallel -AppendTimestamp -SetOctopusServerVersion 

Write-Host "
########################################################################################
#                                                                                      #
#  Local build complete, restart your Octopus Server to test your Calamari changes :)  #
#                                                                                      #
########################################################################################
" -ForegroundColor Cyan