using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

#region Models
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
    [JsonPropertyName("choices")]
    public List<ChoiceData> Choices { get; set; }
}
public class ChoiceData
{
    [JsonPropertyName("code")]
    public string Code { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("min")]
    public int Min { get; set; }
    [JsonPropertyName("max")]
    public int Max { get; set; }
    [JsonPropertyName("garnishItens")]
    public List<GarnishItem> GarnishItens { get; set; }
}

public class GarnishItem
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
#endregion

class Program
{
    const string connString = "Host=localhost;Port=5432;Username=postgres;Password=S4r!nh4;Database=vaidarcerto2";
    const string jsonPath = @"C:\Users\Natha\source\repos\JsonCadastro\JsonCadastro\Sushi.json";

    static void Main()
    {
        while (true)
        {
            Console.WriteLine("\n===== MENU =====");
            Console.WriteLine("1 - Cadastrar (importar JSON)");
            Console.WriteLine("2 - Listar Produtos");
            Console.WriteLine("3 - Atualizar Produto por ID");
            Console.WriteLine("4 - Deletar Produto por ID");
            Console.WriteLine("0 - Sair");
            Console.Write("Escolha: ");
            var op = Console.ReadLine();

            switch (op)
            {
                case "1": Cadastrar(); break;
                case "2": Listar(); break;
                case "3": Atualizar(); break;
                case "4": Deletar(); break;
                case "0": return;
                default: Console.WriteLine("Opção inválida."); break;
            }
        }
    }

    static void Cadastrar()
    {
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine("Arquivo json não encontrado.");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<Root>(json);

        if (root?.Data?.Menu == null)
        {
            Console.WriteLine("JSON inválido ou vazio!");
            return;
        }

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        foreach (var menu in root.Data.Menu)
        {
            foreach (var item in menu.Itens)
            {
                // Product
                int productId;
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO ""Product""(""CompanyId"", ""Sku"", ""Name"", ""Description"", ""ImageUrl"", ""Price"")
                    VALUES (1, @sku, @name, @description, @imageUrl, @price)
                    RETURNING ""Id"";", conn))
                {
                    cmd.Parameters.AddWithValue("sku", item.Code ?? "");
                    cmd.Parameters.AddWithValue("name", item.Description ?? "");
                    cmd.Parameters.AddWithValue("description", item.Details ?? "");
                    cmd.Parameters.AddWithValue("imageUrl", item.LogoUrl ?? "");
                    cmd.Parameters.AddWithValue("price", item.UnitPrice);
                    productId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Category
                int categoryId;
                using (var check = new NpgsqlCommand(@"SELECT ""Id"" FROM ""Category"" WHERE ""Name"" = @name LIMIT 1", conn))
                {
                    check.Parameters.AddWithValue("name", menu.Name ?? "");
                    var result = check.ExecuteScalar();
                    if (result != null) categoryId = Convert.ToInt32(result);
                    else
                    {
                        using var cmd = new NpgsqlCommand(@"
                           INSERT INTO ""Category""(""CompanyId"", ""Name"", ""CreatorId"", ""ImageUrl"")
                           VALUES (1, @name, 1, NULL) RETURNING ""Id"";", conn);
                        cmd.Parameters.AddWithValue("name", menu.Name ?? "");
                        categoryId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }

                // ProductCategory
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO ""ProductCategory""(""CategoryId"", ""ProductId"")
                    VALUES (@categoryId, @productId)", conn))
                {
                    cmd.Parameters.AddWithValue("categoryId", categoryId);
                    cmd.Parameters.AddWithValue("productId", productId);
                    cmd.ExecuteNonQuery();
                }

                // OptionGroups & Options
                if (item.Choices != null)
                {
                    foreach (var choice in item.Choices)
                    {
                        int optionGroupId;
                        using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO ""OptionGroup""(""Name"", ""Description"", ""IsRequired"", ""MinOptions"", ""MaxOptions"")
                            VALUES (@name, '', @isRequired, @min, @max) RETURNING ""Id""", conn))
                        {
                            cmd.Parameters.AddWithValue("name", choice.Name ?? "");
                            cmd.Parameters.AddWithValue("isRequired", choice.Min > 0);
                            cmd.Parameters.AddWithValue("min", choice.Min);
                            cmd.Parameters.AddWithValue("max", choice.Max);
                            optionGroupId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO ""OptionGroupProduct""(""OptionGroupId"", ""ProductId"")
                            VALUES (@ogId, @pId)", conn))
                        {
                            cmd.Parameters.AddWithValue("ogId", optionGroupId);
                            cmd.Parameters.AddWithValue("pId", productId);
                            cmd.ExecuteNonQuery();
                        }

                        if (choice.GarnishItens != null)
                        {
                            foreach (var g in choice.GarnishItens)
                            {
                                using var cmd = new NpgsqlCommand(@"
                                    INSERT INTO ""Option""(""ProductId"", ""OptionGroupId"", ""Name"", ""Description"", ""Price"", ""ImageUrl"", ""Sku"")
                                    VALUES (@pId, @ogId, @name, @desc, @price, @img, @sku)", conn);
                                cmd.Parameters.AddWithValue("pId", productId);
                                cmd.Parameters.AddWithValue("ogId", optionGroupId);
                                cmd.Parameters.AddWithValue("name", g.Description ?? "");
                                cmd.Parameters.AddWithValue("desc", g.Details ?? "");
                                cmd.Parameters.AddWithValue("price", g.UnitPrice);
                                cmd.Parameters.AddWithValue("img", g.LogoUrl ?? "");
                                cmd.Parameters.AddWithValue("sku", g.Code ?? "");
                                cmd.ExecuteNonQuery();
                            }
                        }
                        
                    }
                }
            }
        }
        Console.WriteLine("Importação finalizada com sucesso!");
    }

    static void Listar()
    {
        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"SELECT ""Id"", ""Sku"", ""Name"", ""Price"" FROM ""Product""", conn);
        using var reader = cmd.ExecuteReader();

        Console.WriteLine("\nProdutos cadastrados:");
        while (reader.Read())
        {
            Console.WriteLine($"ID: {reader.GetInt32(0)}, SKU: {reader.GetString(1)}, Nome: {reader.GetString(2)}, Preço: {reader.GetDecimal(3):C}");
        }
        conn.Close();
    }

    static void Atualizar()
    {
        Console.Write("Digite o ID do produto a atualizar: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            Console.WriteLine("ID inválido.");
            return;
        }

        Console.Write("Novo SKU: "); string sku = Console.ReadLine();
        Console.Write("Novo Nome: "); string nome = Console.ReadLine();
        Console.Write("Nova descrição: "); string descricao = Console.ReadLine();
        Console.Write("Nova imagem URL: "); string img = Console.ReadLine();
        Console.Write("Novo preço: "); decimal preco = decimal.Parse(Console.ReadLine());

        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"
            UPDATE ""Product"" SET ""Sku""=@sku, ""Name""=@name, ""Description""=@desc,
            ""ImageUrl""=@img, ""Price""=@price WHERE ""Id""=@id", conn);
        cmd.Parameters.AddWithValue("sku", sku);
        cmd.Parameters.AddWithValue("name", nome);
        cmd.Parameters.AddWithValue("desc", descricao);
        cmd.Parameters.AddWithValue("img", img);
        cmd.Parameters.AddWithValue("price", preco);
        cmd.Parameters.AddWithValue("id", id);

        int qtd = cmd.ExecuteNonQuery();
        Console.WriteLine(qtd > 0 ? "Atualizado com sucesso!" : "Produto não encontrado.");
        conn.Close();
    }

    static void Deletar()
    {
        Console.Write("Digite o ID do produto para deletar: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            Console.WriteLine("ID inválido.");
            return;
        }

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        // 1) Deletar opções (Option)
        using (var cmd = new NpgsqlCommand(@"DELETE FROM ""Option"" WHERE ""ProductId""=@id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }

        // 2) Deletar vinculo OptionGroupProduct
        using (var cmd = new NpgsqlCommand(@"DELETE FROM ""OptionGroupProduct"" WHERE ""ProductId""=@id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }

        // 3) Deletar ProductCategory
        using (var cmd = new NpgsqlCommand(@"DELETE FROM ""ProductCategory"" WHERE ""ProductId""=@id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }

        // (se quiser também apagar OptionGroup, caso não seja usado em outros produtos)
        using (var cmd = new NpgsqlCommand(@"DELETE FROM ""OptionGroup"" WHERE ""Id"" IN (SELECT ""OptionGroupId"" FROM ""OptionGroupProduct"" WHERE ""ProductId""=@id)", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }

        // 4) Por fim, apaga o Product
        using (var cmd = new NpgsqlCommand(@"DELETE FROM ""Product"" WHERE ""Id""=@id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            int r = cmd.ExecuteNonQuery();
            Console.WriteLine(r > 0 ? "Deletado com sucesso!" : "Produto não encontrado.");
        }
    }

}
