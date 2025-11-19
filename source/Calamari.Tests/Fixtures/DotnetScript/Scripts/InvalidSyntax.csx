#r "RestSharp.dll"

using RestSharp;

public class DoATHing
{
    public void Hi()
    {
        var client = new RestClient("https://pokeapi.co/api/v2/");
        var request = new RestRequest("pokemon/ditto")
        var response = await client.ExecuteGetAsync(request);
        Console.WriteLine(response.Content);
    }
}