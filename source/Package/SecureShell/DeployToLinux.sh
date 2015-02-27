#! /bin/bash -e
# Octopus Linux deployment script
# Version: 1.0.0
# ------------------------------------------------------------------------------
#
# This script is used to control how we deploy packages to Linux
#
PROGNAME=$(basename $0)

# Ensure that our tools folder is on the PATH
export TOOLSPATH=$(dirname $0)
TOOLSPATH=$(pwd)${TOOLSPATH:1}

case ":$PATH:" in
	*:$TOOLSPATH:*) ;;
	*) PATH=$PATH:$TOOLSPATH ;;
esac

export TEMPFILESPATH=$1

echo "Starting deploy"

echo "Setting up Octopus variables"
Octopus_Directory=$(tentacle get "Octopus.Action.Package.Ssh.RootDirectoryPath")
Octopus_Action_Ssh_PackagesDirectoryPath=$(tentacle get "Octopus.Action.Package.Ssh.PackagesDirectoryPath")
Octopus_Action_Ssh_PackageFileName=$(tentacle get "Octopus.Action.Package.Ssh.PackageFileName")
Octopus_Package=$(tentacle get "Octopus.Action.Package.NuGetPackageId")
Octopus_Package_Version=$(tentacle get "Octopus.Action.Package.NuGetPackageVersion")
Octopus_Action_Ssh_ApplicationsDirectoryPath=$(tentacle get "Octopus.Action.Package.Ssh.ApplicationsDirectoryPath")
Octopus_Project=$(tentacle get "Octopus.Project.Name")
Octopus_Environment=$(tentacle get "Octopus.Environment.Name")
set +e # disable errexit as the following variables are not required
Octopus_CustomInstallationDirectory=$(tentacle get "Octopus.Action.Package.CustomInstallationDirectory")
Octopus_CustomInstallationDirectoryShouldBePurgedBeforeDeployment=$(tentacle get "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment")
set -e # enable errexit so that we exit if an error occurs

#
# create application installation directory
#
Application="${Octopus_Package%.tar.gz}"
Octopus_Action_Ssh_ApplicationsDirectoryPath=$Octopus_Action_Ssh_ApplicationsDirectoryPath/$Octopus_Environment/$Octopus_Project/$Application
if [ ! -d "$Octopus_Action_Ssh_ApplicationsDirectoryPath" ]
then
	echo "$Octopus_Action_Ssh_ApplicationsDirectoryPath does not exist, creating..."
	mkdir -p $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

cd $Octopus_Action_Ssh_ApplicationsDirectoryPath

#
# create extract folder ~/.tentacle/apps/{environmentname}/{packagename}/{version[{-n}]}
# e.g. ~/.tentacle/apps/dev/lib/0.0.1-2 (i.e. lib version 0.0.1 deployment 2)
#
n=
Octopus_Package_Version_Deployment=$Octopus_Package_Version
while [ -d "$Octopus_Package_Version_Deployment" ]
do
	n=`expr ${n:-0} + 1`
	Octopus_Package_Version_Deployment=$Octopus_Package_Version-$n
done
Octopus_Package_Version=$Octopus_Package_Version_Deployment
mkdir -p $Octopus_Package_Version

Octopus_Action_Ssh_ApplicationsDirectoryPath=$Octopus_Action_Ssh_ApplicationsDirectoryPath/$Octopus_Package_Version
echo $(tentacle set "OctopusOriginalPackageDirectoryPath" "${Octopus_Action_Ssh_ApplicationsDirectoryPath:${#HOME}+1}")

#
# extract package
#
echo "Extracting $Octopus_Action_Ssh_PackagesDirectoryPath/$Octopus_Action_Ssh_PackageFileName to $Octopus_Action_Ssh_ApplicationsDirectoryPath"

tar -xzvf $Octopus_Action_Ssh_PackagesDirectoryPath/$Octopus_Action_Ssh_PackageFileName -C $Octopus_Action_Ssh_ApplicationsDirectoryPath -m

echo $(tentacle set "Octopus.Tentacle.CurrentDeployment.PackageFilePath" "${Octopus_Action_Ssh_PackagesDirectoryPath:${#HOME}+1}/$Octopus_Action_Ssh_PackageFileName")

cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
#
# run pre-deploy script
#
predeploy="$Octopus_Action_Ssh_ApplicationsDirectoryPath/PreDeploy.sh"
if [ -f $predeploy ]
then
	echo "Running pre-deploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $predeploy
	. $predeploy
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "PreDeploy script failed" $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

customPre="$TEMPFILESPATH/CustomScriptConvention.PreDeploy.sh"
if [ -f $customPre ]
then
	echo "Running embedded pre-deploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $customPre
	. $customPre
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "embedded pre-deploy script failed" $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

#
# if a custom install directory has been specified,
# check if directory should be purged, purge if set,
# copy files
#
if [ -n "$Octopus_CustomInstallationDirectory" ]
then
	# turn off case sensitive string matching
	shopt -s nocasematch
	if [[ "$Octopus_CustomInstallationDirectoryShouldBePurgedBeforeDeployment" == "true" ]]
	then
		echo "Purging the directory $Octopus_CustomInstallationDirectory"
		rm -rf $Octopus_CustomInstallationDirectory/*
	fi
	# turn on case sensitive string matching again
	shopt -u nocasematch

	echo "Custom install directory has been specified: $Octopus_CustomInstallationDirectory"
	if [ ! -d "$Octopus_CustomInstallationDirectory" ]
	then
		mkdir -p $Octopus_CustomInstallationDirectory
	fi

	#
	# copy application files to install directory
	#
	echo "Copying files from $Octopus_Action_Ssh_ApplicationsDirectoryPath to $Octopus_CustomInstallationDirectory..."
	cp -a $Octopus_Action_Ssh_ApplicationsDirectoryPath/* $Octopus_CustomInstallationDirectory
	cd $Octopus_CustomInstallationDirectory
	Octopus_Action_Ssh_ApplicationsDirectoryPath=$Octopus_CustomInstallationDirectory
fi

#
# run deploy script
#
deploy="$Octopus_Action_Ssh_ApplicationsDirectoryPath/Deploy.sh"
if [ -f $deploy ]
then
	echo "Running Deploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $deploy
	. $deploy
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "Deploy script failed." $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

custom="$TEMPFILESPATH/CustomScriptConvention.Deploy.sh"
if [ -f $custom ]
then
	echo "Running embedded deploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $custom
	. $custom
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "embedded deploy script failed" $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

#
# run post-deploy script
#
postdeploy="$Octopus_Action_Ssh_ApplicationsDirectoryPath/PostDeploy.sh"
if [ -f $postdeploy ]
then
	echo "Running PostDeploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $postdeploy
	. $postdeploy
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "PostDeploy script failed" $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

customPost="$TEMPFILESPATH/CustomScriptConvention.PostDeploy.sh"
if [ -f $customPost ]
then
	echo "Running embedded post-deploy script"
	# Give only the script owner read/execute permission
	chmod u+rx $customPost
	. $customPost
	if [ $? -ne 0 ]
	then
		error_exit $PROGNAME $LINENO "embedded post-deploy script failed" $?
	fi

	# restore the current directory
	cd $Octopus_Action_Ssh_ApplicationsDirectoryPath
fi

echo "Finished deploy"