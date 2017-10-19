using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcwidPoc
{
    class Program
    {
        static void Main(string[] args)
        {
            var auth = OauthClient.AskForAuthorization("abcd0123", "123", TimeSpan.FromSeconds(30));
        }
    }
}
