using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Npgsql;
using System.Text.Json.Serialization;

public class Root
{
    [JsonPropertyName("data")]
    public DataObj Data { get; set; }
}

public class DataObj
{
    [JsonPropertyName("menu")]
    public List<MenuData> Menu { get; set; }
}

public class MenuData
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("itens")]
    public List<ItemData> Itens { get; set; }
}

public class ItemData
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("details")]
    public string Details { get; set; }

    [JsonPropertyName("logoUrl")]
    public string LogoUrl { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
}


class Program
{
    static void Main()
    {

        string jsonPath = @"C:\Users\Natha\source\repos\JsonCadastro\JsonCadastro\Sushi.json";


        if (!File.Exists(jsonPath))
        {
            Console.WriteLine("Arquivo Sushi.json não encontrado!");
            return;
        }

        
        string json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<Root>(json);

        if (root?.Data?.Menu == null)
        {
            Console.WriteLine("JSON inválido ou vazio!");
            return;
        }

        string connString = "Host=localhost;Port=5432;Username=postgres;Password=S4r!nh4;Database=19";

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        foreach (var menu in root.Data.Menu)
        {
            foreach (var item in menu.Itens)
            {
                string sql = @"
                    INSERT INTO ""Product""
                    (""CompanyId"", ""Sku"", ""Name"", ""Description"", ""ImageUrl"", ""Price"")
                    VALUES (@companyId, @sku, @name, @description, @imageUrl, @price)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("companyId", 1);
                cmd.Parameters.AddWithValue("sku", item.Code ?? "");
                cmd.Parameters.AddWithValue("name", item.Description ?? "");
                cmd.Parameters.AddWithValue("description", item.Details ?? "");
                cmd.Parameters.AddWithValue("imageUrl", item.LogoUrl ?? "");
                cmd.Parameters.AddWithValue("price", item.UnitPrice);

                cmd.ExecuteNonQuery();
            }
        }

        Console.WriteLine("Importação concluída com sucesso!");
    }
}
