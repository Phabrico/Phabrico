param(
    $applicationProjectName, 
    $applicationExecutable,
    $applicationBuildPath,
    $applicationDescription
)

$disallowedFileNames = @("Phabrico.data")

Write-Host "Building with the following parameters: Project ="$applicationProjectName "  Executable ="$applicationExecutable "  Directory of content ="$applicationBuildPath "  Description=" $applicationDescription

# set some constant parameters
$componentsFileName = 'Generated\PreBuildGeneratedWiXComponents.wxi'
$componentRefsFileName = 'Generated\PreBuildGeneratedWiXComponentRefs.wxi'

# get all files from output directory from Application project
$files = gci -r -af -Path $applicationBuildPath
$fileIndex = 0

# create a subdirectory Generated if it doesn't exist yet
if(!(Test-Path -Path "Generated" )){
    New-Item -ItemType directory -Path "Generated"
}

# create components and componentrefs wix include files
[System.IO.File]::WriteAllText($componentsFileName, "<?xml version=""1.0"" encoding=""utf-8""?>`r`n<Include xmlns:util=""http://schemas.microsoft.com/wix/UtilExtension"">`r`n")
[System.IO.File]::WriteAllText($componentRefsFileName, "<?xml version=""1.0"" encoding=""utf-8""?>`r`n<Include>`r`n")

# generate for each file found in the output directory of the Application project a reference to the components and componentrefs wix include files
foreach ($file in $files) {
    # check if file is allowed to be added to MSI package
    $fileName = [System.IO.Path]::GetFileName($file.Name)
    if ($disallowedFileNames.Contains($fileName)) { continue; }

    # increase file sequence number
    $fileIndex++

    # generate a component ref name which is used to identify the file (for referencing between the 2 wix include files)
    $componentId = [string]::Format("component{0}", $fileIndex)

    # generate a GUID to identify the file as a unique component
    $componentGuid = [guid]::NewGuid()

    # generate a unique id for the file itself
    $fileId = [string]::Format("id{0}", [guid]::NewGuid().ToString().Replace("-", "_"))

    # retrieve the relative name of the directory where the file is located in
    $directoryName = [System.IO.Path]::GetDirectoryName($file.FullName).Replace($applicationBuildPath + "\", "")

    # initialize WIX tags for service installation
    $componentParameters = "";

    # check if the file is located in the root directory
    if ($directoryName -eq $applicationBuildPath) {
        # file is in root directory: define file as a component

        #log filename
        Write-Host "Adding file to MSI package: " $file.FullName

        # set filename
        $fileName = [string]::Format("`$(var." + $applicationProjectName + ".TargetDir){0}", [System.IO.Path]::GetFileName($file.FullName))

        if ([System.IO.Path]::GetFileName($file.FullName) -ieq "$applicationExecutable.config") {
            # app.config file
            $componentParameters = "`
                <util:XmlFile Id=""TcpListenPort"" 
                              Action=""setValue""
                              File=""[#$fileId]""
                              SelectionLanguage=""XPath"" 
                              Permanent=""yes""
                              ElementPath=""//appSettings/add[\[]@key='TcpListenPort'[\]]""
                              Name=""value"" 
                              Value=""[APPCONFIG_TCPLISTENPORT]"" />
                <util:XmlFile Id=""DatabaseDirectory"" 
                              Action=""setValue""
                              File=""[#$fileId]""
                              SelectionLanguage=""XPath"" 
                              Permanent=""yes""
                              ElementPath=""//appSettings/add[\[]@key='DatabaseDirectory'[\]]""
                              Name=""value"" 
                              Value=""[APPCONFIG_DATABASEDIRECTORY]"" />
            ";
        }

        # in case the filename is the application's executable itself, use 'APPLICATION' as fileId
        # The 'APPLICATION' fileId will also be used in the details tab of the properties of the MSI file
        if ([System.IO.Path]::GetFileName($file.FullName) -ieq $applicationExecutable) {
            $fileId = 'APPLICATION'
            $componentParameters = "`
                <ServiceInstall Id=""" + [System.IO.Path]::GetFileName($file.Name) + "Installer"" 
                        Name=""$applicationProjectName"" 
                        Type=""ownProcess""
                        Vital=""yes""
                        DisplayName=""$applicationProjectName""
                        Description=""$applicationDescription""
                        Start=""auto""
                        Account=""LocalSystem""
                        ErrorControl=""normal""
                        Interactive=""no"" />
                <ServiceControl Id=""StartService""
                        Name=""$applicationProjectName""
                        Stop=""both""
                        Start=""install""
                        Remove=""uninstall""
                        Wait=""yes"" />
            "
        }

        # generate the line that needs to be added to the wix component include file
        $line = "        <Component Id=""$componentId"" Guid=""$componentGuid"">`
            <File Id=""$fileId"" Source=""$fileName"" KeyPath=""yes"" />$componentParameters`
        </Component>`r`n"
    }
    else
    {
        # file is in subdirectory: define directory as component

        # generate a unique id for the directory where the file is located in
        $directoryId = [string]::Format("id{0}", [guid]::NewGuid().ToString().Replace("-", "_"))

        #log filename
        Write-Host "Adding file to MSI package: " $file.FullName

        # set filename
        $fileName = [string]::Format("`$(var." + $applicationProjectName + ".TargetDir)\$directoryName\{0}", [System.IO.Path]::GetFileName($file.FullName))

        # in case the filename is the application's executable itself, use 'APPLICATION' as fileId
        # The 'APPLICATION' fileId will also be used in the details tab of the properties of the MSI file
        if ([System.IO.Path]::GetFileName($file.FullName) -ieq $applicationExecutable) {
            $fileId = 'APPLICATION'
            $componentParameters = "`
                <ServiceInstall Id=""" + [System.IO.Path]::GetFileName($file.Name) + "Installer"" 
                        Name=""$applicationProjectName"" 
                        Type=""ownProcess""
                        Vital=""yes""
                        DisplayName=""$applicationProjectName""
                        Description=""$applicationDescription""
                        Start=""auto""
                        Account=""LocalSystem""
                        ErrorControl=""normal""
                        Interactive=""no"" />
                <ServiceControl Id=""StartService""
                        Name=""$applicationProjectName""
                        Stop=""both""
                        Start=""install""
                        Remove=""uninstall""
                        Wait=""yes"" />
            "
        }

        # generate the line that needs to be added to the wix component include file
        $line = "        <Directory Id=""$directoryId"" Name=""$directoryName"">`
            <Component Id=""$componentId"" Guid=""$componentGuid"">`
                <File Id=""$fileId"" Source=""$fileName"" KeyPath=""yes"" />$componentParameters`
            </Component>`
        </Directory>`r`n"
    }

    # append generated line to wix component include file
    [System.IO.File]::AppendAllText($componentsFileName, $line)

    # append line with componentId to wix componentrefs include file
    $line = "            <ComponentRef Id=""$componentId"" />`r`n"
    [System.IO.File]::AppendAllText($componentRefsFileName, $line)
}

# finish both wix include files
[System.IO.File]::AppendAllText($componentsFileName, "</Include>")
[System.IO.File]::AppendAllText($componentRefsFileName, "</Include>")
