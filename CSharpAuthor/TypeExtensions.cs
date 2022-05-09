using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpAuthor
{
    public static class TypeExtensions
    {
        public static string GetGenericName(this Type type)
        {
            var charIndex = type.Name.IndexOf('`');

            if (charIndex >= 0)
            {
                return type.Name.Substring(0, charIndex);
            }

            return type.Name;
        }
    }
}
