using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MusicDrucker
{
    class MusicJob
    {
        public String user { get; private set; }
        public String status { get; private set; }
        public String details { get; private set; }
        public String title { get; private set; }
        public String size { get; private set; }

        public MusicJob(String user, String status, String details, String title, String size)
        {
            this.user = user;
            this.status = status;
            this.details = details;
            this.title = title;
            this.size = size;
        }
    }
}
