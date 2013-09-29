#I @"tools/FAKE/tools/"
#r @"FakeLib.dll"

#I "./tools/FSharp.Formatting/lib/net40"
#r "System.Web.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.CodeFormat.dll"
#load "./tools/FSharp.Formatting/literate/literate.fsx"

open System.IO
open Fake
open FSharp.Literate
 
// properties 
let projectName = "FAKE"
let projectSummary = "FAKE - F# Make - Get rid of the noise in your build scripts."
let projectDescription = "FAKE - F# Make - is a build automation tool for .NET. Tasks and dependencies are specified in a DSL which is integrated in F#."
let authors = ["Steffen Forkmann"; "Mauricio Scheffer"; "Colin Bull"]
let mail = "forkmann@gmx.de"
let homepage = "http://github.com/forki/fake"
  
let buildDir = "./build"
let testDir = "./test"
let deployDir = "./Publish"
let docsDir = "./docs" 
let nugetDir = "./nuget" 
let reportDir = "./report" 
let deployZip = deployDir @@ sprintf "%s-%s.zip" projectName buildVersion
let packagesDir = "./packages"

let isLinux =
    int System.Environment.OSVersion.Platform |> fun p ->
        (p = 4) || (p = 6) || (p = 128)

// Targets
Target "Clean" (fun _ -> CleanDirs [buildDir; testDir; deployDir; docsDir; nugetDir; reportDir])

Target "RestorePackages" RestorePackages

Target "CopyFSharpFiles" (fun _ ->
    ["./tools/FSharp/FSharp.Core.optdata"
     "./tools/FSharp/FSharp.Core.sigdata"]
      |> CopyTo buildDir
)

open Fake.AssemblyInfoFile

Target "SetAssemblyInfo" (fun _ ->
    CreateFSharpAssemblyInfo "./src/app/FAKE/AssemblyInfo.fs"
        [Attribute.Title "FAKE - F# Make Command line tool"
         Attribute.Guid "fb2b540f-d97a-4660-972f-5eeff8120fba"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]

    CreateFSharpAssemblyInfo "./src/app/Fake.Deploy/AssemblyInfo.fs"
        [Attribute.Title "FAKE - F# Make Deploy tool"
         Attribute.Guid "413E2050-BECC-4FA6-87AA-5A74ACE9B8E1"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]

    CreateFSharpAssemblyInfo "./src/deploy.web/Fake.Deploy.Web.App/AssemblyInfo.fs"
        [Attribute.Title "FAKE - F# Make Deploy Web App"
         Attribute.Guid "2B684E7B-572B-41C1-86C9-F6A11355570E"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]

    CreateFSharpAssemblyInfo "./src/deploy.web/Fake.Deploy.Web/AssemblyInfo.cs"
        [Attribute.Title "FAKE - F# Make Deploy Web"
         Attribute.Guid "27BA7705-3F57-47BE-B607-8A46B27AE876"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]

    CreateFSharpAssemblyInfo "./src/app/FakeLib/AssemblyInfo.fs"
        [Attribute.Title "FAKE - F# Make Lib"
         Attribute.Guid "d6dd5aec-636d-4354-88d6-d66e094dadb5"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]

    CreateFSharpAssemblyInfo "./src/app/Fake.SQL/AssemblyInfo.fs"
        [Attribute.Title "FAKE - F# Make SQL Lib"
         Attribute.Guid "A161EAAF-EFDA-4EF2-BD5A-4AD97439F1BE"
         Attribute.Product "FAKE - F# Make"
         Attribute.Version buildVersion
         Attribute.FileVersion buildVersion]
)

Target "BuildSolution" (fun _ ->        
    MSBuildWithDefaults "Build" ["./FAKE.sln"]
    |> Log "AppBuild-Output: "
)

Target "GenerateDocumentation" (fun _ ->
    let source = "./help"
    let template = "./tools/FSharp.Formatting/literate/templates/template-project.html"
    let projInfo =
      [ "page-description", "FAKE - F# Make"
        "page-author", "Steffen Forkmann"
        "github-link", "https://github.com/fsharp/FAKE"
        "project-name", "FAKE - F# Make" ]

    Literate.ProcessDirectory (source, template, docsDir, replacements = projInfo)

    CopyDir (docsDir @@ "content") "tools/FSharp.Formatting/literate/content" allFiles  

    (* Temporary disable tests on *nix, bug # 122 *)
    if not isLinux then
        !! (buildDir @@ "Fake*.dll")
        |> Docu (fun p ->
            {p with
                ToolPath = buildDir @@ "docu.exe"
                TemplatesPath = @".\tools\docu\templates\"
                OutputPath = docsDir })
)

Target "CopyDocu" (fun _ -> 
    ["./tools/docu/docu.exe"
     "./tools/docu/DocuLicense.txt"]
       |> CopyTo buildDir
)

Target "CopyLicense" (fun _ -> 
    ["License.txt"
     "README.markdown"
     "changelog.markdown"]
       |> CopyTo buildDir
)

Target "BuildZip" (fun _ ->     
    !+ (buildDir @@ @"**/*.*") 
    -- "*.zip" 
    -- "**/*.pdb"
      |> Scan
      |> Zip buildDir deployZip
)

Target "Test" (fun _ ->
    (* Temporary disable tests on *nix, bug # 122 *)
    if not isLinux then
        let MSpecVersion = GetPackageVersion packagesDir "Machine.Specifications"
        let mspecTool = sprintf @"%s/Machine.Specifications.%s/tools/mspec-clr4.exe" packagesDir MSpecVersion

        !! (testDir @@ "Test.*.dll") 
        |> MSpec (fun p -> 
                {p with
                    ToolPath = mspecTool
                    ExcludeTags = ["HTTP"]
                    HtmlOutputDir = reportDir})
)

Target "ZipDocumentation" (fun _ -> 
    (* Temporary disable tests on *nix, bug # 122 *)
    if not isLinux then
        !! (docsDir @@ @"**/*.*")  
           |> Zip docsDir (deployDir @@ sprintf "Documentation-%s.zip" buildVersion)
)

Target "CreateNuGet" (fun _ -> 
    (* Temporary disable tests on *nix, bug # 122 *)
    if not isLinux then
        let nugetDocsDir = nugetDir @@ "docs"
        let nugetToolsDir = nugetDir @@ "tools"

        CopyDir nugetDocsDir docsDir allFiles  
        CopyDir nugetToolsDir buildDir allFiles
        CopyDir nugetToolsDir @"./lib/fsi" allFiles
        DeleteFile (nugetToolsDir @@ "Gallio.dll")

        NuGet (fun p -> 
            {p with
                Authors = authors
                Project = projectName
                Description = projectDescription                               
                OutputPath = nugetDir
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                Publish = hasBuildParam "nugetkey" }) "fake.nuspec"
)

///<summary>Cleans a directory by removing all files and sub-directories.</summary>
///<param name="path">The path of the directory to clean.</param>
///<user/>
let CleanGitDir path =
    let di = directoryInfo path
    if di.Exists then
        logfn "Deleting contents of %s" path
        // delete all files
        Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
          |> Seq.iter (fun file -> 
                let fi = fileInfo file
                fi.IsReadOnly <- false
                fi.Delete())
    
        // deletes all subdirectories
        let rec deleteDirs actDir =
            let di = directoryInfo actDir
            if di.Name = ".git" then () else
            Directory.Delete(actDir,true)
    
        Directory.GetDirectories path 
          |> Seq.iter deleteDirs      
    else
        CreateDir path
    
    // set writeable
    File.SetAttributes(path,FileAttributes.Normal)        


/// Runs the git command and returns the first line of the result
let runSimpleGitCommand repositoryDir command =
    try
        let ok,msg,errors = Git.CommandHelper.runGitCommand repositoryDir command
        if msg.Count = 0 then "" else
        try
            msg.[0]
        with 
        | exn -> failwithf "Git didn't return a msg.\r\n%s" errors
    with 
    | exn -> failwithf "Could not run \"git %s\".\r\nError: %s" command exn.Message

Target "UpdateDocs" (fun _ ->
    CleanDir "gh-pages"
    Git.CommandHelper.runSimpleGitCommand "" "clone -b gh-pages --single-branch git@github.com:fsharp/FAKE.git gh-pages" |> printfn "%s"
    
    CleanGitDir "gh-pages"
    CopyRecursive "docs" "gh-pages" true |> printfn "%A"
    runSimpleGitCommand "gh-pages" "add . --all" |> printfn "%s"
    runSimpleGitCommand "gh-pages" (sprintf """commit -m "Update generated documentation for version %s""" buildVersion) |> printfn "%s"
    Git.Branches.push "gh-pages"    
)

Target "Default" DoNothing

// Dependencies
"Clean"
    ==> "RestorePackages"
    ==> "CopyFSharpFiles"
    =?> ("SetAssemblyInfo",not isLocalBuild ) 
    ==> "BuildSolution"
    ==> "Test"
    ==> "CopyLicense" <=> "CopyDocu"
    ==> "BuildZip"
    ==> "GenerateDocumentation"
    ==> "ZipDocumentation"
    ==> "CreateNuGet"
    ==> "Default"

// start build
RunTargetOrDefault "Default"
