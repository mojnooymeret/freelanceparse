using System;
using System.Collections.Generic;
using System.Text;

namespace freelanceparse
{
   internal class User
   {
      public int id { get; set; }
      public string user_id { get; set; }
      public string categories { get; set; }
      public string sub { get; set; }
      public User(int id, string user_id, string categories, string sub)
      {
         this.id = id;
         this.user_id = user_id;
         this.categories = categories;
         this.sub = sub;
      }
   }
}
