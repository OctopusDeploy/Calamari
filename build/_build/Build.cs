using System;
using Nuke.Common;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.BuildLocal);

    Target CheckForbiddenWords => _ => _.Executes(() =>
                                                  {
                                                      //TODO:
                                                  });

    Target Clean => _ => _.Executes(() =>
                                    {
                                        //TODO:
                                    });

    Target Restore => _ => _.DependsOn(Clean)
                            .Executes(() =>
                                      {
                                          //TODO:
                                      });

    Target BuildCode => _ => _.DependsOn(CheckForbiddenWords)
                              .DependsOn(Restore)
                              .Executes(async () =>
                                        {
                                            //TODO:
                                        });
    Target PackBinaries => _ => _.DependsOn(BuildCode)
                                 .Executes(async () =>
                                           {
                                               //TODO:
                                           });

    Target PackTests => _ => _.DependsOn(BuildCode)
                              .Executes(async () =>
                                        {
                                            //TODO:
                                        });
    Target Pack => _ => _.DependsOn(PackBinaries)
                         .DependsOn(PackTests);

    Target CopyToLocalPackages => _ => _
                                      //TODO: What's the Nuke equiv of .WithCriteria(BuildSystem.IsLocalBuild)
                                      .Executes(() =>
                                                {
                                                    //TODO:
                                                });

    Target SetOctopusServerVersion => _ => _
                                          //TODO:     .WithCriteria(BuildSystem.IsLocalBuild)
                                          //TODO:     .WithCriteria(setOctopusServerVersion)
                                          .Executes(() =>
                                                    {
                                                        //TODO:
                                                    });

    Target SetTeamCityVersion => _ => _.Executes(() =>
                                                 {
                                                     //TODO:
                                                 });

    Target BuildLocal => _ => _.DependsOn(PackBinaries)
                               .DependsOn(CopyToLocalPackages)
                               .DependsOn(SetOctopusServerVersion);


    Target BuildCI => _ => _.DependsOn(SetTeamCityVersion)
                            .DependsOn(Pack)
                            .DependsOn(CopyToLocalPackages)
                            .DependsOn(SetOctopusServerVersion);
}