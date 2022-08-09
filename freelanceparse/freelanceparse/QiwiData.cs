using System;
using System.Collections.Generic;
using System.Text;

namespace freelanceparse
{
   internal class QiwiData
   {
      public int id { get; set; }
      public string title { get; set; }
      public string mean { get; set; }
      public QiwiData(int id, string title, string mean)
      {
         this.id = id;
         this.title = title;
         this.mean = mean;
      }
   }
}
