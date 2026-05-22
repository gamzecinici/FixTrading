using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FixTrading.Persistence;
using FixTrading.Common.Dtos.Instrument;
using Microsoft.Extensions.Configuration;

var builder = new DbContextOptionsBuilder<AppDbContext>();
var connectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=1234";
builder.UseNpgsql(connectionString);

using var context = new AppDbContext(builder.Options);
try {
    var count = context.Instruments.Count();
    Console.WriteLine($"Instruments count: {count}");
    var symbols = context.Instruments.Select(i => i.Symbol).ToList();
    Console.WriteLine($"Symbols: {string.Join(", ", symbols)}");
} catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
}
