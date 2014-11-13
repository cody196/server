using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenWorld.DAL
{
    abstract class DataObject<T> where T : DataObject<T>
    {
        public string _id { get; set; }
        public string _rev { get; set; }

        public async Task<bool> SaveAsync()
        {
            return await Data<T>.SaveObjectAsync((T)this);
        }
    }
}
