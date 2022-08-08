using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace freelanceparse
{
   internal class ConnectDB
   {
      public static SQLiteDataReader Query(string str)
      {
         SQLiteConnection SQLiteConnection = new SQLiteConnection("Data Source=|DataDirectory|freelancedata.db");
         SQLiteCommand SQLiteCommand = new SQLiteCommand(str, SQLiteConnection);
         try {
            SQLiteConnection.Open();
            SQLiteDataReader reader = SQLiteCommand.ExecuteReader();
            return reader;
         } catch { return null; }
      }

      public static void LoadData(List<User> data)
      {
         data.Clear();
         SQLiteDataReader query = Query("select * from `User`;");
         if (query != null) {
            while (query.Read()) {
               data.Add(new User(
                  Convert.ToInt32(query.GetValue(0)),
                  query.GetValue(1).ToString(),
                  query.GetValue(2).ToString(),
                  query.GetValue(3).ToString()
               ));
            }
         }
      }

      public static void LoadQiwi(List<QiwiData> qiwi)
      {
         qiwi.Clear();
         SQLiteDataReader query = Query("select * from `QiwiData`;");
         if (query != null) {
            while (query.Read()) {
               qiwi.Add(new QiwiData(
                  Convert.ToInt32(query.GetValue(0)),
                  query.GetValue(1).ToString(),
                  query.GetValue(2).ToString()
               ));
            }
         }
      }

      public static void LoadOrder(List<Order> orders)
      {
         orders.Clear();
         SQLiteDataReader query = Query("select * from `Order`;");
         if (query != null) {
            while (query.Read()) {
               orders.Add(new Order(
                  Convert.ToInt32(query.GetValue(0)),
                  query.GetValue(1).ToString(),
                  query.GetValue(2).ToString(),
                  query.GetValue(3).ToString(),
                  query.GetValue(4).ToString(),
                  query.GetValue(5).ToString()
               ));
            }
         }
      }
   }
}
