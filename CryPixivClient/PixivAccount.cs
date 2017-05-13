using Pixeez;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient
{
    public class PixivAccount
    {
        public string Username { get; set; }
        public bool IsLoggedIn { get; set; }

        Tokens tokens;

        public PixivAccount(string username)
        {
            Username = username;
        }

        public async Task<Tuple<bool, string>> Login(string password)
        {
            try
            {
                tokens = await Auth.AuthorizeAsync(Username, password);

                IsLoggedIn = true;
                return new Tuple<bool, string>(true, "Success");
            }
            catch(Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
        }
    }
}
