using System;
using System.Net.Http;
using System.Threading.Tasks;
using BlazorApp3;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Http; // ? for BrowserHttpMessageHandler
using Microsoft.Extensions.DependencyInjection;

namespace BlazorApp3.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);



            await builder.Build().RunAsync();
        }
    }
}
