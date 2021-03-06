using System.Collections.Generic;
using System.Linq;

namespace Leaves.Client.DataContracts
{
    public class GetLeavesContract
    {
        public IList<GetLeavesItemContract> Items { get; set; }

        public static GetLeavesContract Empty
            => new GetLeavesContract {
                Items = Enumerable.Empty<GetLeavesItemContract>().ToList()
            };
    }
}
