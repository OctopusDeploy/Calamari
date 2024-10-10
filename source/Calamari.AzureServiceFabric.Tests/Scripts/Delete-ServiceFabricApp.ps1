$ConnectArgs = @{
    ConnectionEndpoint = "#{Octopus.Action.ServiceFabric.ConnectionEndpoint}"
    X509Credential = $True
    StoreLocation = "#{Octopus.Action.ServiceFabric.CertificateStoreLocation}"
    StoreName = "#{Octopus.Action.ServiceFabric.CertificateStoreName}"
    ServerCertThumbprint = "#{Octopus.Action.ServiceFabric.ServerCertThumbprint}"
    ServerCommonName = "#{Certificates-1.SubjectCommonName}"
    FindType = "FindByThumbprint"
    FindValue = "#{Certificates-1.Thumbprint}"
}

$applicationName = "#{ApplicationName}"

Connect-ServiceFabricCluster @ConnectArgs

$nodes = Get-ServiceFabricNode

foreach($node in $nodes)
{
    $replicas = Get-ServiceFabricDeployedReplica -NodeName $node.NodeName -ApplicationName $applicationName

    foreach ($replica in $replicas)
    {
        Remove-ServiceFabricReplica -ForceRemove -NodeName $node.NodeName -PartitionId $replica.Partitionid -ReplicaOrInstanceId $replica.ReplicaOrInstanceId
    }
}