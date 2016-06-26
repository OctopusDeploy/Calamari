let value = 
    match "Name" |> Octopus.tryFindVariable with
        | Some x -> x
        | None -> "not available"

printfn "Hello %s" value
