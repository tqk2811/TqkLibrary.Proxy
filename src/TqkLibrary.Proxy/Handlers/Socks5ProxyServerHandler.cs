using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;

namespace TqkLibrary.Proxy.Handlers
{
    public class Socks5ProxyServerHandler : BaseProxyServerHandler
    {
        readonly Socks5ProxyServerHandler? _parent;
        public Socks5ProxyServerHandler()
        {

        }
        public Socks5ProxyServerHandler(Socks5ProxyServerHandler parent) : base(parent)
        {
            _parent = parent;
        }

        public virtual Task<Socks5_Auth> ChoseAuthAsync(IEnumerable<Socks5_Auth> socks5_Auths, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.ChoseAuthAsync(socks5_Auths, cancellationToken);
            else return Task.FromResult(_ChoseAuthAsync(socks5_Auths, cancellationToken));
        }




        /*=========================================================================*/


        Socks5_Auth _ChoseAuthAsync(IEnumerable<Socks5_Auth> socks5_Auths, CancellationToken cancellationToken = default)
        {
            if (socks5_Auths is not null && socks5_Auths.Any())
            {
                foreach (var socks5_Auth in socks5_Auths)
                {
                    switch (socks5_Auth)
                    {
                        case Socks5_Auth.NoAuthentication:
                            return socks5_Auth;

                        default:
                            continue;
                    }
                }
            }
            return Socks5_Auth.Reject;
        }
    }
}
