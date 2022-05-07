using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CSharpAuthor.Tests.TypeDefinitionTests
{
    public class GetTests
    {
        [Fact]
        public void GenericClassGet()
        {
            var definition = TypeDefinition.Get(typeof(Task<string>));

            Assert.NotNull(definition);

            Assert.Equal("Task", definition.Name);
            Assert.Equal("System.Threading", definition.Namespace);
        }
    }
}
