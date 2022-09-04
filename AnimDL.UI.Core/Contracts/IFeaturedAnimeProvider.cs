using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimDL.UI.Core.Contracts;

public interface IFeaturedAnimeProvider
{
    IObservable<IEnumerable<FeaturedAnime>> GetFeaturedAnime();
}
