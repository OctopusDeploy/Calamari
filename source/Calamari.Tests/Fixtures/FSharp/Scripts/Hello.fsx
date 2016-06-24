let tryFindVariable = Octopus.tryFindVariable Octopus.variables
let value = 
    match "Name" |> tryFindVariable with
        | Some x -> x
        | None -> "not available"

printfn "Hello %s" value
