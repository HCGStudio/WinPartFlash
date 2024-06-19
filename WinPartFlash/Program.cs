// See https://aka.ms/new-console-template for more information

using HitRefresh.MobileSuit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinPartFlash;

Console.WriteLine("This program comes with absolute no warranty. Backup your data before using this tool.");
Console.WriteLine("WARNING: PARTITION COUNT STARTS FROM 0 IN THIS TOOL. (MIGHT CHANGE IN THE FUTURE)");

var builder = Suit.CreateBuilder();

builder.Services.AddSingleton<PartFlashService>();

builder.MapClient<WinPartFlashClient>();

await builder.Build().RunAsync();