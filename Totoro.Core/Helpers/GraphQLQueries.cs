namespace Totoro.Core.Helpers
{
    internal static class GraphQLQueries
    {

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
