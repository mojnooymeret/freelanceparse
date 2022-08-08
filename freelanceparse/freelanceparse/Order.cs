using System;
using System.Collections.Generic;
using System.Text;

namespace freelanceparse
{
   internal class Order
   {
      public int id { get; set; }
      public string title { get; set; }
      public string category { get; set; }
      public string price {get;set;}
      public string text {get;set;}
      public string order_id { get; set; }
      public Order(int id, string title, string category, string price, string text, string order_id)
      {
         this.id = id;
         this.title = title;
         this.category = category;
         this.price = price;
         this.text = text;
         this.order_id = order_id;
      }
   }
}
