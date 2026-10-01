module.exports = {
    timezone: "Australia/Brisbane",
    enabledManagers: ["nuget", "github-actions"],
    platform: "github",
    repositories: ["OctopusDeploy/Calamari"],

    // Limit the amount of PRs created
    prConcurrentLimit: 10,
    prHourlyLimit: 5
}
