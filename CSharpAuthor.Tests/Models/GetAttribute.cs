using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpAuthor.Tests.Models;

public class GetAttribute : Attribute
{
    public string? Path { get; set; }
}