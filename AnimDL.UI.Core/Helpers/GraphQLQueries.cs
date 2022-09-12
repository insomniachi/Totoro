using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimDL.UI.Core.Helpers
{
    internal static class GraphQLQueries
    {
        public static string GetAniListIdFromMal()
        {
            return @"
                    query Media($idMal: Int)
                    {
                        Media(idMal: $idMal)
                        {
                            id
                        }
                    }
                    ".Trim();
        }

        public static string GetTimeStamps()
        {
            return @"
                    query Shows($serviceId: String!)
                    {
                        findShowsByExternalId(service: ANILIST, serviceId: $serviceId)
                        {
                            id
                            name
                            episodes
                            {
                                number
                                name
                                timestamps
                                {
                                    type 
                                    {
                                        description
                                    }
                                    at
                                }
                            }
                        }
                    }
                    ".Trim();
        }
    }
}
