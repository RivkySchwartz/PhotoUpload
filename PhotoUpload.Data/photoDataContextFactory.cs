using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoUpload.Data;

public class photoDataContextFactory : IDesignTimeDbContextFactory<photoDataContext>
{
    public photoDataContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
           .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), 
           $"..{Path.DirectorySeparatorChar}PhotoUpload.Web"))
           .AddJsonFile("appsettings.json")
           .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true).Build();

        return new photoDataContext(config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("ConStr connection string is missing."));
    }
}