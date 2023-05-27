using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Totoro.Plugins.Torrents.AnimeTosho;

internal static class Extensions
{
    internal static string ToQueryParamter(this Filter filter)
    {
        return filter switch
        {
            Filter.None => "",
            Filter.NoRemake => "remake",
            Filter.TrustedOnly => "trusted",
            _ => ""
        };
    }

    internal static string ToQueryParamter(this Sort sort)
    {
        return sort switch
        {
            Sort.OldestFirst => "date-a",
            Sort.NewestFirst => "",
            Sort.SmallestFirst => "size-a",
            Sort.LargestFirst => "size-d",
            _ => ""
        };
    }
}
