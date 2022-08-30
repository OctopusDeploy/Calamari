let findVariableOrDefault = "default value" |> Octopus.findVariableOrDefault
let value = "84C7692E-AD89-41F5-9987-DA4C555D2813" |> findVariableOrDefault
printfn "Hello %s" value
