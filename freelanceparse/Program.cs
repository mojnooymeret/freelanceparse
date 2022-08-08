using Qiwi.BillPayments;
using Qiwi.BillPayments.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace freelanceparse
{
   internal class Program
   {
      static TimerCallback callbackParse = new TimerCallback(ParseUser);
      static TimerCallback callbackSub = new TimerCallback(CheckSub);
      private static int lastCheck = 0;
      private static string token { get; set; } = "5533741121:AAE0Lo-FdkAxUGhi9vQGBPCeYkWQVGb6v1Q";
      private static TelegramBotClient client;
      static void Main(string[] args)
      {
         client = new TelegramBotClient(token);
         Timer timer = new Timer(callbackParse, null, 0, 300000);
         Timer subs = new Timer(callbackSub, null, 0, 600000);
         client.StartReceiving();
         client.OnMessage += ClientMessage;
         client.OnCallbackQuery += async (object sc, CallbackQueryEventArgs ev) => {
            InlineButtonOperation(sc, ev);
         };
         Console.ReadLine();
      }

      public static List<User> users = new List<User>();
      public static List<QiwiData> qiwidata = new List<QiwiData>();
      public static List<Order> orders = new List<Order>();


      private static async void CheckSub(object state)
      {
         if (DateTime.Now.Hour == 0 && lastCheck != DateTime.Now.Day) {
            ConnectDB.LoadData(users);
            for (int i = 0; i < users.Count; i++) {
               try {
                  string sub = users[i].sub;
                  if (sub != "none" && sub != "") {
                     DateTime date = DateTime.Now;
                     double days = (Convert.ToDateTime(sub) - date).TotalDays;
                     if (days < 0) ConnectDB.Query("update User set sub = 'none' where user_id = " + users[i].user_id + ";");
                  }
               } catch { }
            }
            lastCheck = DateTime.Now.Day;
         }
      }

      private static async void ClientMessage(object sender, MessageEventArgs e)
      {
         try {
            var message = e.Message;
            if (message.Text == "/start") {
               ConnectDB.LoadData(users);
               try {
                  string id = string.Empty;
                  for (int i = 0; i < users.Count; i++) if (users[i].user_id == message.Chat.Id.ToString()) { id = "+"; return; }
                  if (id != "+") ConnectDB.Query("insert into User (user_id, categories, sub) values ('" + message.Chat.Id.ToString() + "', 'none', 'none');");
                  ConnectDB.LoadQiwi(qiwidata);
                  await client.SendTextMessageAsync(message.Chat.Id, qiwidata[2].mean);
               } catch { }
            }
            else if (message.Text == "/parse") {
               ParseUser(message.Chat.Id.ToString());
            }
            else if (message.Text == "/menu") {
               try {
                  var keyboard = new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("🛒 Категории"), new KeyboardButton("🧾 Подписка") } }, true);
                  await client.SendTextMessageAsync(message.Chat.Id, "✅ Кнопки меню загружены", replyMarkup: keyboard);
               } catch { }
            }
            else if (message.Chat.Id.ToString() == "885185553" && message.Text.Contains("/givesub") || message.Chat.Id.ToString() == "2101261084" && message.Text.Contains("/givesub")) {
               if (message.Text.Contains(" ")) {
                  ConnectDB.LoadData(users);
                  string id = string.Empty;
                  try {
                     id = users.Find(item => item.user_id == message.Text.Split(' ')[1]).id.ToString();
                  } catch { }
                  if (id != "" && id != null && id != string.Empty) {
                     ConnectDB.Query("update User set sub = '" + DateTime.Now.AddDays(31) + "' where id = " + id + ";");
                     await client.SendTextMessageAsync(message.Chat.Id, "✅ Пользователю " + message.Text.Split(' ')[1] + " выдана подписка");
                  }
                  else await client.SendTextMessageAsync(message.Chat.Id, "⛔ Пользователь не найден");
               }
               else await client.SendTextMessageAsync(message.Chat.Id, "⛔ Неверный формат команды (Пример: /givesub 12345678");
            }
            else if (message.Chat.Id.ToString() == "885185553" && message.Text.Contains("/takesub") || message.Chat.Id.ToString() == "2101261084" && message.Text.Contains("/takesub")) {
               if (message.Text.Contains(" ")) {
                  ConnectDB.LoadData(users);
                  string sub = string.Empty;
                  try {
                     sub = users.Find(item => item.user_id == message.Text.Split(' ')[1]).sub;
                  } catch { }
                  if (sub != "" && sub != null && sub != string.Empty && users.Find(item => item.user_id == message.Text.Split(' ')[1]).user_id != "2101261084" && users.Find(item => item.user_id == message.Text.Split(' ')[1]).user_id != "885185553") {
                     if (sub == "none") {
                        ConnectDB.Query("update User set sub = 'none' where id = " + message.Text.Split(' ')[1] + ";");
                        await client.SendTextMessageAsync(message.Chat.Id, "✅ Подписка пользователя " + message.Text.Split(' ')[1] + " удалена");
                     }
                     else await client.SendTextMessageAsync(message.Chat.Id, "У пользователя уже " + message.Text.Split(' ')[1] + " нет подписки");
                  }
                  else await client.SendTextMessageAsync(message.Chat.Id, "⛔ Пользователь не найден");
               }
               else await client.SendTextMessageAsync(message.Chat.Id, "⛔ Неверный формат команды (Пример: /takesub 12345678");
            }
            else if (message.Chat.Id.ToString() == "885185553" && message.Text.Contains("/amountsub") || message.Chat.Id.ToString() == "2101261084" && message.Text.Contains("/amountsub")) {
               ConnectDB.LoadQiwi(qiwidata);
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Изменить стоимость", "ChangeAmount") } });
               await client.SendTextMessageAsync(message.Chat.Id, "Текущая стоимость подписки: " + qiwidata[0].mean + " руб.", replyMarkup: keyborad);
            }
            else if (message.Chat.Id.ToString() == "885185553" && message.Text.Contains("/secretkey") || message.Chat.Id.ToString() == "2101261084" && message.Text.Contains("/secretkey")) {
               ConnectDB.LoadQiwi(qiwidata);
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Изменить ключ", "ChangeKey") } });
               await client.SendTextMessageAsync(message.Chat.Id, "Текущий секретный ключ: " + qiwidata[1].mean, replyMarkup: keyborad);
            }
            else if (message.Chat.Id.ToString() == "885185553" && message.Text.Contains("/hello") || message.Chat.Id.ToString() == "2101261084" && message.Text.Contains("/hello")) {
               ConnectDB.LoadQiwi(qiwidata);
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Изменить приветствие", "ChangeHello") } });
               await client.SendTextMessageAsync(message.Chat.Id, "Текущее приветствие: " + qiwidata[2].mean, replyMarkup: keyborad);
            }
            else if (message.Text == "🧾 Подписка") {
               ConnectDB.LoadData(users);
               try {
                  var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Оформить подписку", "GoSub") } });
                  string sub = users.Find(item => item.user_id == message.Chat.Id.ToString()).sub;
                  if (sub != "none") await client.SendTextMessageAsync(message.Chat.Id, "Подписка активна до " + sub);
                  else await client.SendTextMessageAsync(message.Chat.Id, "У вас нет подписки на бота", replyMarkup: keyborad);
               } catch { }
            }
            else if (message.Text == "🛒 Категории") {
               try {
                  var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Мои категории", "MyCategory") }, new[] { InlineKeyboardButton.WithCallbackData("Добавить категорию", "AddCategory") } });
                  await client.SendTextMessageAsync(message.Chat.Id, message.Text, replyMarkup: keyborad);
               } catch { }
            }
            else if (message.ReplyToMessage != null && message.ReplyToMessage.Text.Contains("Для удаления введите цифру(-ы) категории через запятую")) {
               try {
                  ConnectDB.LoadData(users);
                  string request = string.Empty;
                  if (message.Text.Contains(",")) {
                     string[] delete = message.Text.Split(',');
                     string[] categories = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories.Split('|');
                     for (int i = 0; i < categories.Length; i++) {
                        for (int j = 0; j < delete.Length; j++) {
                           if (j == i) categories[j] = null;
                        }
                     }
                     for (int i = 0; i < categories.Length; i++) if (categories[i] != null) request += categories[i] + "|";
                     request = request.Trim('|');
                     ConnectDB.Query("update User set categories = '" + request + "' where user_id = '" + message.Chat.Id.ToString() + "';");
                  }
                  else {
                     string categories = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories;
                     if (categories.Contains('|')) {
                        string[] temp = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories.Split('|');
                        for (int i = 0; i < temp.Length; i++) {
                           if (i == Convert.ToInt32(message.Text) - 1) {
                              temp[i] = null;
                           }
                        }
                        for (int i = 0; i < temp.Length; i++) if (temp[i] != null && temp[i] != "") request += temp[i] + "|";
                        request = request.Trim('|');
                        ConnectDB.Query("update User set categories = '" + request + "' where user_id = '" + message.Chat.Id.ToString() + "';");
                     }
                  }
                  ConnectDB.LoadData(users);
                  string categoriesCheck = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories;
                  if (categoriesCheck == "" || categoriesCheck == "null" || categoriesCheck == " ") ConnectDB.Query("update User set categories = 'none' where user_id = " + message.Chat.Id.ToString() + ";");
                  await client.SendTextMessageAsync(message.Chat.Id, "✅ Указанные категории успешно удалены");
               } catch { await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Ошибка удаления категории"); }
            }
            else if (message.ReplyToMessage != null && message.ReplyToMessage.Text.Contains("Введите новую стоимость подписки")) {
               int amount = 0;
               try {
                  amount = Convert.ToInt32(message.Text);
               } catch { await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Неверный формат стоимости"); return; }
               ConnectDB.Query("update QiwiData set mean = '" + amount.ToString() + "' where id = 1;");
               await client.SendTextMessageAsync(message.Chat.Id, "✅ Стоимость подписки изменена на " + amount.ToString());
            }
            else if (message.ReplyToMessage != null && message.ReplyToMessage.Text.Contains("Введите новый секретный ключ")) {
               string key = message.Text;
               var qiwi = BillPaymentsClientFactory.Create(secretKey: key);
               try {
                  var qiwiBill = qiwi.CreateBill(info: new Qiwi.BillPayments.Model.In.CreateBillInfo {
                     BillId = message.Chat.Id.ToString(),
                     Amount = new Qiwi.BillPayments.Model.MoneyAmount {
                        ValueDecimal = 1,
                        CurrencyEnum = Qiwi.BillPayments.Model.CurrencyEnum.Rub
                     },
                     Comment = "Подписка на Kwork | Биржа",
                     ExpirationDateTime = DateTime.Now.AddMinutes(1),
                  });
               } catch { await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Нерабочий секретный ключ"); return; }
               ConnectDB.Query("update QiwiData set mean = '" + key + "' where id = 2;");
               await client.SendTextMessageAsync(message.Chat.Id, "✅ Секретный ключ кошелька успешно изменен на: " + key);
            }
            else if (message.ReplyToMessage != null && message.ReplyToMessage.Text.Contains("Введите новое приветствие")) {
               string hello = string.Empty;
               try {
                  hello = message.Text;
               } catch { await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Неверный формат стоимости"); return; }
               ConnectDB.Query("update QiwiData set mean = '" + hello + "' where id = 3;");
               await client.SendTextMessageAsync(message.Chat.Id, "✅ Приветствие изменено на \"" + hello + "\"");
            }
         } catch { }
      }

      private static async void InlineButtonOperation(object sc, CallbackQueryEventArgs ev)
      {
         Random rnd = new Random();
         var message = ev.CallbackQuery.Message;
         var data = ev.CallbackQuery.Data;
         string bill = rnd.Next(100000000, 999999999).ToString() + message.Chat.Id.ToString() + rnd.Next(100000000, 999999999).ToString();
         if (data == "GoSub") {
            ConnectDB.LoadQiwi(qiwidata);
            try {
               var qiwi = BillPaymentsClientFactory.Create(
               secretKey: qiwidata[1].mean
            );
               var qiwiBill = qiwi.CreateBill(
                  info: new Qiwi.BillPayments.Model.In.CreateBillInfo {
                     BillId = bill,
                     Amount = new Qiwi.BillPayments.Model.MoneyAmount {
                        ValueDecimal = 1/*Convert.ToInt32(qiwidata[0].mean)*/,
                        CurrencyEnum = Qiwi.BillPayments.Model.CurrencyEnum.Rub
                     },
                     Comment = "Подписка на Kwork | Биржа",
                     ExpirationDateTime = DateTime.Now.AddHours(1),
                  }
               );
               await client.SendTextMessageAsync(message.Chat.Id, "💵 Для оплаты подписки перейдите по ссылке: \n" + qiwiBill.PayUrl.ToString());
               var status = "WAITING";
               while (status == "WAITING") {
                  Thread.Sleep(15000);
                  var qiwiStatus = qiwi.GetBillInfo(billId: bill);
                  status = qiwiStatus.Status.ValueString;
               }
               if (status == "PAID") {
                  ConnectDB.Query("update User set sub = '" + DateTime.Now.AddDays(31).Date + "' where user_id = " + message.Chat.Id.ToString() + ";");
                  await client.SendTextMessageAsync(message.Chat.Id, "✅ Подписка успешно оплачена");
               }
               else if (status == "REJECTED") {
                  qiwi.CancelBill(message.Chat.Id.ToString());
                  await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Счёт оплаты подписки отклонен");
               }
               else if (status == "EXPIRED") {
                  qiwi.CancelBill(message.Chat.Id.ToString());
                  await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Срок действия оплаты подписки истек");
               }
            } catch (Exception ex) { await client.SendTextMessageAsync(message.Chat.Id, "⛔️ На данный момент оплата подписки невозможна " + ex.Message); return; }
         }
         else if (data.Contains("FullText_")) {
            try {
               ConnectDB.LoadOrder(orders);
               string text = orders.Find(item => item.order_id == data.Split('_')[1]).text;
               await client.SendTextMessageAsync(message.Chat.Id, text);
            } catch { }
         }
         else if (data == "ChangeAmount") {
            try {
               await client.SendTextMessageAsync(message.Chat.Id, "Введите новую стоимость подписки", replyMarkup: new ForceReplyMarkup { Selective = true });
            } catch { }
         }
         else if (data == "ChangeKey") {
            try {
               await client.SendTextMessageAsync(message.Chat.Id, "Введите новый секретный ключ", replyMarkup: new ForceReplyMarkup { Selective = true });
            } catch { }
         }
         else if (data == "ChangeHello") {
            try {
               await client.SendTextMessageAsync(message.Chat.Id, "Введите новое приветствие", replyMarkup: new ForceReplyMarkup { Selective = true });
            } catch { }
         }
         else if (data == "AddCategory") {
            ConnectDB.LoadData(users);
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Дизайн", "CategoryDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Разработка и IT", "CategoryDevIT") }, new[] { InlineKeyboardButton.WithCallbackData("Тексты и переводы", "CategoryTextTranslate") }, new[] { InlineKeyboardButton.WithCallbackData("SEO и трафик", "CategorySEOTrafic") }, new[] { InlineKeyboardButton.WithCallbackData("Соцсети и реклама", "CategoryOnlineAds") }, new[] { InlineKeyboardButton.WithCallbackData("Аудио, видео, съемка", "CategoryAudioVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Бизнес и жизнь", "CategoryBusiness") } });
               await client.SendTextMessageAsync(message.Chat.Id, "🛒 Выберите категорию", replyMarkup: keyborad);
            } catch { }
         }
         else if (data == "MyCategory") {
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Удалить категории", "DeleteCategory") } });
               await client.DeleteMessageAsync(message.Chat.Id, message.MessageId);
               string categories = string.Empty;
               ConnectDB.LoadData(users);
               string id = users.Find(item => item.user_id == message.Chat.Id.ToString()).user_id;
               for (int i = 0; i < users.Count; i++) {
                  if (users[i].user_id == id) {
                     string[] userCategory = users[i].categories.Split('|');
                     if (userCategory.Length > 0 && userCategory[0] != "none") {
                        categories = "Ваши категории:\n";
                        for (int j = 0; j < userCategory.Length; j++) {
                           categories += Convert.ToInt32(j + 1) + ". " + userCategory[j].Split('‼')[0] + "\n";
                        }
                     }
                     else categories = "У вас нет выбранных категорий";
                  }
               }
               if (categories == "У вас нет выбранных категорий") await client.SendTextMessageAsync(message.Chat.Id, categories);
               else await client.SendTextMessageAsync(message.Chat.Id, categories, replyMarkup: keyborad);
            } catch { }
         }
         else if (data == "DeleteCategory") {
            try {
               await client.SendTextMessageAsync(message.Chat.Id, "Для удаления введите цифру(-ы) категории через запятую", replyMarkup: new ForceReplyMarkup { Selective = true });
            } catch { }
         }
         else if (data == "MainCategory") {
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Дизайн", "CategoryDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Разработка и IT", "CategoryDevIT") }, new[] { InlineKeyboardButton.WithCallbackData("Тексты и переводы", "CategoryTextTranslate") }, new[] { InlineKeyboardButton.WithCallbackData("SEO и трафик", "CategorySEOTraficc") }, new[] { InlineKeyboardButton.WithCallbackData("Соцсети и реклама", "CategoryOnlineAds") }, new[] { InlineKeyboardButton.WithCallbackData("Аудио, видео, съемка", "CategoryAudioVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Бизнес и жизнь", "CategoryBusiness") } });
               await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
            } catch { }
         }
         else if (data == "CategoryDesign") {
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Логотип и брендинг", "UnCategoryLogo") }, new[] { InlineKeyboardButton.WithCallbackData("Веб и мобильный дизайн", "UnCategoryWebDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Арт и иллюстрации", "UnCategoryArt") }, new[] { InlineKeyboardButton.WithCallbackData("Полиграфия", "UnCategoryPolygraph") }, new[] { InlineKeyboardButton.WithCallbackData("Интерьер и экстрьер", "UnCategoryInter") }, new[] { InlineKeyboardButton.WithCallbackData("Промышленный дизайн", "UnCategoryProm") }, new[] { InlineKeyboardButton.WithCallbackData("Презентация и инфографика", "UnCategoryPresent") }, new[] { InlineKeyboardButton.WithCallbackData("Обработка и редактирование", "UnCategoryEdit") }, new[] { InlineKeyboardButton.WithCallbackData("Наружная реклама", "UnCategoryAd") }, new[] { InlineKeyboardButton.WithCallbackData("Маркетплейсы и соцсети", "UnCategoryMarket") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Дизайн\"", "UnCategoryAllDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
               await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
            } catch { }

         }
         else if (data == "CategoryDevIT") {
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Доработка и настройка сайта", "UnCategoryDevSite") }, new[] { InlineKeyboardButton.WithCallbackData("Создание сайта", "UnCategorySite") }, new[] { InlineKeyboardButton.WithCallbackData("Скрипты и боты", "UnCategoryScript") }, new[] { InlineKeyboardButton.WithCallbackData("Верстка", "UnCategoryVerstka") }, new[] { InlineKeyboardButton.WithCallbackData("Десктоп программирование", "UnCategoryDesk") }, new[] { InlineKeyboardButton.WithCallbackData("Мобильные приложения", "UnCategoryMobile") }, new[] { InlineKeyboardButton.WithCallbackData("Игры", "UnCategoryGames") }, new[] { InlineKeyboardButton.WithCallbackData("Сервера и хостинг", "UnCategoryServers") }, new[] { InlineKeyboardButton.WithCallbackData("Юзабилити, тесты и помощь", "UnCategoryUse") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Разработка и IT\"", "UnCategoryAllDevIT") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
               await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
            } catch { }

         }
         else if (data == "CategoryTextTranslate") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Резюме и вакансии", "UnCategoryResume") }, new[] { InlineKeyboardButton.WithCallbackData("Набор текста", "UnCategoryText") }, new[] { InlineKeyboardButton.WithCallbackData("Продающие бизнес-тексты", "UnCategoryBusinessText") }, new[] { InlineKeyboardButton.WithCallbackData("Тексты и наполнение сайта", "UnCategoryTextSite") }, new[] { InlineKeyboardButton.WithCallbackData("Переводы", "UnCategoryTranslate") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Тексты и переводы\"", "AllCategoryTransText") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "CategorySEOTraficc") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Трафик", "UnCategoryTrafic") }, new[] { InlineKeyboardButton.WithCallbackData("Семантическое ядро", "UnCategoryThread") }, new[] { InlineKeyboardButton.WithCallbackData("Ссылки", "UnCategorySources") }, new[] { InlineKeyboardButton.WithCallbackData("Статистика и аналтика", "UnCategoryAnaletic") }, new[] { InlineKeyboardButton.WithCallbackData("SEO аудиты, консультации", "UnCategorySEOAudit") }, new[] { InlineKeyboardButton.WithCallbackData("Внутренняя оптимизация", "UnCategoryOptimization") }, new[] { InlineKeyboardButton.WithCallbackData("Продвижение сайта в топ", "UnCategoryTopSite") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"SEO и трафик\"", "UnCategoryAllSEOTrafic") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "CategoryOnlineAds") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Базы данных и клиентов", "UnCategoryDBUsers") }, new[] { InlineKeyboardButton.WithCallbackData("Маркетплейсы и доски объявлений", "UnCategoryMarketAds") }, new[] { InlineKeyboardButton.WithCallbackData("E-mail маркетинг", "UnCategoryEMailMarketing") }, new[] { InlineKeyboardButton.WithCallbackData("Контекстная реклама", "UnCategoryContextAdvert") }, new[] { InlineKeyboardButton.WithCallbackData("Реклама и PR", "UnCategoryAdvertPR") }, new[] { InlineKeyboardButton.WithCallbackData("Соцсети и SMM", "UnCategorySMM") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Соцсети и реклама\"", "UnCategoryAllOnlineAds") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "CategoryAudioVideo") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Редактирование аудио", "UnCategoryEditAudio") }, new[] { InlineKeyboardButton.WithCallbackData("Видеосъемка и монтаж", "UnCategoryVideomotage") }, new[] { InlineKeyboardButton.WithCallbackData("Интро и анимация логотипа", "UnCategoryIntroLogo") }, new[] { InlineKeyboardButton.WithCallbackData("Видеоролики", "UnCategoryVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Музыка и песни", "UnCategoryMusicSong") }, new[] { InlineKeyboardButton.WithCallbackData("Аудиозапись и озвучка", "UnCategoryOzv") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Видео, аудио, съемка\"", "UnCategoryAllAudioVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "CategoryBusiness") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Продажа сайтов", "UnCategorySellSite") }, new[] { InlineKeyboardButton.WithCallbackData("Персональный помощник", "UnCategoryHelper") }, new[] { InlineKeyboardButton.WithCallbackData("Бухгалтерия и налоги", "UnCategoryBuhNalog") }, new[] { InlineKeyboardButton.WithCallbackData("Юридическая помощь", "UnCategoryYrHelp") }, new[] { InlineKeyboardButton.WithCallbackData("Обучение и консалтинг", "UnCategoryLesson") }, new[] { InlineKeyboardButton.WithCallbackData("Обзвоны и продажи", "UnCategoryCallSell") }, new[] { InlineKeyboardButton.WithCallbackData("Подбор персонала", "UnCategoryFindEmp") }, new[] { InlineKeyboardButton.WithCallbackData("Стройка и ремонт", "UnCategoryRepair") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Бизнес и жизнь\"", "UnCategoryAllBusiness") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "MainCategory") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         #region design
         else if (data == "UnCategoryLogo") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Логотипы", "UnUnCategoryLogo") }, new[] { InlineKeyboardButton.WithCallbackData("Фирменный стиль", "UnUnCategoryCompanyStyle") }, new[] { InlineKeyboardButton.WithCallbackData("Брендирование и сувенирка", "UnUnCategoryBrand") }, new[] { InlineKeyboardButton.WithCallbackData("Визитки", "UnUnCategoryVisit") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Логотип и брендинг\"", "UnUnCategoryAllLogoBrand") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryWebDesign") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Мобильный дизайн", "UnUnCategoryMobileDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Email-дизайн", "UnUnCategoryEMailDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Веб-дизайн", "UnUnCategoryWebDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Баннеры и иконки", "UnUnCategoryBannersIcon") }, new[] { InlineKeyboardButton.WithCallbackData("Брендирование и сувенирка", "UnUnCategoryBrand") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Веб и мобильный дизайн\"", "UnUnCategoryAllWebDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryArt") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Иллюстрации и рисунки", "UnUnCategoryArts") }, new[] { InlineKeyboardButton.WithCallbackData("Тату, принты", "UnUnCategoryTato") }, new[] { InlineKeyboardButton.WithCallbackData("Дизайн игр", "UnUnCategoryGameDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Готовые шаблоны и рисунки", "UnUnCategoryCompleteArt") }, new[] { InlineKeyboardButton.WithCallbackData("Портрет, шарж, карикатура", "UnUnCategoryPortret") }, new[] { InlineKeyboardButton.WithCallbackData("Стикеры", "UnUnCategoryStickers") }, new[] { InlineKeyboardButton.WithCallbackData("NFT арт", "UnUnCategoryNFT") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Арт и иллюстрации\"", "UnUnCategoryAllArt") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryPolygraph") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Брошюра и буклет", "UnUnCategoryBouklet") }, new[] { InlineKeyboardButton.WithCallbackData("Листовка и флаер", "UnUnCategoryPaperFlaer") }, new[] { InlineKeyboardButton.WithCallbackData("Плакат и афиша", "UnUnCategoryAfisha") }, new[] { InlineKeyboardButton.WithCallbackData("Календарь и открытка", "UnUnCategoryCalendar") }, new[] { InlineKeyboardButton.WithCallbackData("Каталог, меню, книга", "UnUnCategoryCatalogBook") }, new[] { InlineKeyboardButton.WithCallbackData("Грамота и сертификат", "UnUnCategorySertificate") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Полиграфия\"", "UnUnCategoryAllPolygraph") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryInter") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Интерьер", "UnUnCategoryInterier") }, new[] { InlineKeyboardButton.WithCallbackData("Дизайн домов и сооружений", "UnUnCategoryHomeDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Ландшафтный дизайн", "UnUnCategoryLandshaft") }, new[] { InlineKeyboardButton.WithCallbackData("Дизайн мебели", "UnUnCategoryMebel") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Интерьер и экстерьер\"", "UnUnCategoryAllInter") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryProm") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Упаковка и этикета", "UnUnCategoryEtiket") }, new[] { InlineKeyboardButton.WithCallbackData("Электроника и устройства", "UnUnCategoryElectron") }, new[] { InlineKeyboardButton.WithCallbackData("Предметы и аксессуары", "UnUnCategoryItems") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Промышленный дизайн\"", "UnUnCategoryAllProm") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryPresent") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Презентации", "UnUnCategoryPresentacia") }, new[] { InlineKeyboardButton.WithCallbackData("Инфографика", "UnUnCategoryInfographica") }, new[] { InlineKeyboardButton.WithCallbackData("Карта и схема", "UnUnCategoryMaphSchemes") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Презентация и инфографика\"", "UnUnCategoryAllPresent") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryEdit") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Отрисовка в векторе", "UnUnCategoryVector") }, new[] { InlineKeyboardButton.WithCallbackData("3D-графика", "UnUnCategory3D") }, new[] { InlineKeyboardButton.WithCallbackData("Фотомонтаж и обработка", "UnUnCategoryPhotomotage") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Обработка и редактирование\"", "UnUnCategoryAllEdit") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryAd") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Билборды и стенды", "UnUnCategoryBuildboard") }, new[] { InlineKeyboardButton.WithCallbackData("Витрины и вывески", "UnUnCategoryVitrins") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Наружная реклама\"", "UnUnCategoryAllAd") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryMarket") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Дизайн в соцсетях", "UnUnCategorySocDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Дизайн для маркетплейсов", "UnUnCategoryMarketplaceDesign") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Маркетплейсы и соцсети\"", "UnUnCategoryAllMarket") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDesign") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryAllDesign") AddCategory(message, "https://kwork.ru/projects?c=15", "Дизайн");
         else if (data == "UnUnCategoryAllLogoBrand") AddCategory(message, "https://kwork.ru/projects?view=0&c=25", "Логотип и брендинг");
         else if (data == "UnUnCategoryAllWebDesign") AddCategory(message, "https://kwork.ru/projects?view=0&c=24", "Веб и мобильный дизайн");
         else if (data == "UnUnCategoryAllArt") AddCategory(message, "https://kwork.ru/projects?view=0&c=28", "Арт и иллюстрации");
         else if (data == "UnUnCategoryAllPolygraph") AddCategory(message, "https://kwork.ru/projects?view=0&c=27", "Полиграфия");
         else if (data == "UnUnCategoryAllInter") AddCategory(message, "https://kwork.ru/projects?view=0&c=90", "Интерьер и экстерьер");
         else if (data == "UnUnCategoryAllProm") AddCategory(message, "https://kwork.ru/projects?view=0&c=250", "Промышленный дизайн");
         else if (data == "UnUnCategoryAllPresent") AddCategory(message, "https://kwork.ru/projects?view=0&c=270", "Презентации и инфографика");
         else if (data == "UnUnCategoryAllEdit") AddCategory(message, "https://kwork.ru/projects?view=0&c=68", "Обработка и редактирование");
         else if (data == "UnUnCategoryAllAd") AddCategory(message, "https://kwork.ru/projects?view=0&c=272", "Наружная реклама");
         else if (data == "UnUnCategoryAllMarket") AddCategory(message, "https://kwork.ru/projects?view=0&c=286", "Маркетплейсы и соцсети");
         else if (data == "UnUnCategoryLogo") AddCategory(message, "https://kwork.ru/projects?c=25&attr=401928", "Логотипы");
         else if (data == "UnUnCategoryCompanyStyle") AddCategory(message, "https://kwork.ru/projects?c=25&attr=401976", "Фирменный стиль");
         else if (data == "UnUnCategoryBrand") AddCategory(message, "https://kwork.ru/projects?c=25&attr=402005", "Брендирование и сувенирка");
         else if (data == "UnUnCategoryVisit") AddCategory(message, "https://kwork.ru/projects?c=25&attr=402019", "Визитки");
         else if (data == "UnUnCategoryMobileDesign") AddCategory(message, "https://kwork.ru/projects?c=24&attr=71", "Мобильный дизайн");
         else if (data == "UnUnCategoryEMailDesign") AddCategory(message, "https://kwork.ru/projects?c=24&attr=75", "Email-дизайн");
         else if (data == "UnUnCategoryWebDesign") AddCategory(message, "https://kwork.ru/projects?c=24&attr=393348", "Веб-дизайн");
         else if (data == "UnUnCategoryBannersIcon") AddCategory(message, "https://kwork.ru/projects?c=24&attr=398966", "Банеры и иконки");
         else if (data == "UnUnCategoryArts") AddCategory(message, "https://kwork.ru/projects?c=28&attr=819", "Иллюстрации и рисунки");
         else if (data == "UnUnCategoryTato") AddCategory(message, "https://kwork.ru/projects?c=28&attr=361968", "Тату, принты");
         else if (data == "UnUnCategoryGameDesign") AddCategory(message, "https://kwork.ru/projects?c=28&attr=391843", "Дизайн игр");
         else if (data == "UnUnCategoryCompleteArt") AddCategory(message, "https://kwork.ru/projects?c=28&attr=396640", "Готовые рисунки и шаблоны");
         else if (data == "UnUnCategoryPortret") AddCategory(message, "https://kwork.ru/projects?c=28&attr=398135", "Портрет, шарж, карикатура");
         else if (data == "UnUnCategoryStickers") AddCategory(message, "https://kwork.ru/projects?c=28&attr=411950", "Стикеры");
         else if (data == "UnUnCategoryNFT") AddCategory(message, "https://kwork.ru/projects?c=28&attr=1273394", "NFT арт");
         else if (data == "UnUnCategoryBouklet") AddCategory(message, "https://kwork.ru/projects?c=27&attr=1245", "Брошюра и буклет");
         else if (data == "UnUnCategoryPaperFlaer") AddCategory(message, "https://kwork.ru/projects?c=27&attr=1246", "Листовка и флаер");
         else if (data == "UnUnCategoryAfisha") AddCategory(message, "https://kwork.ru/projects?c=27&attr=1247", "Плакат и афиша");
         else if (data == "UnUnCategoryCalendar") AddCategory(message, "https://kwork.ru/projects?c=27&attr=1248", "Календарь и открытка");
         else if (data == "UnUnCategoryCartalogBook") AddCategory(message, "https://kwork.ru/projects?c=27&attr=1249", "Каталог, меню, книга");
         else if (data == "UnUnCategorySertificate") AddCategory(message, "https://kwork.ru/projects?c=27&attr=7838", "Грамота и сертификат");
         else if (data == "UnUnCategoryInterier") AddCategory(message, "https://kwork.ru/projects?c=90&attr=206908", "Интерьер");
         else if (data == "UnUnCategoryHomeDesign") AddCategory(message, "https://kwork.ru/projects?c=90&attr=206911", "Дизайн домов и сооружений");
         else if (data == "UnUnCategoryLandshaft") AddCategory(message, "https://kwork.ru/projects?c=90&attr=206914", "Ландшафтный дизайн");
         else if (data == "UnUnCategoryMebel") AddCategory(message, "https://kwork.ru/projects?c=90&attr=206915", "Дизайн мебели");
         else if (data == "UnUnCategoryEtiket") AddCategory(message, "https://kwork.ru/projects?c=250&attr=399791", "Упаковка и этикетка");
         else if (data == "UnUnCategoryElectron") AddCategory(message, "https://kwork.ru/projects?c=250&attr=399794", "Электроника и устройства");
         else if (data == "UnUnCategoryItems") AddCategory(message, "https://kwork.ru/projects?c=250&attr=399797", "Предметы и аксессуары");
         else if (data == "UnUnCategoryPresentacia") AddCategory(message, "https://kwork.ru/projects?c=270&attr=314398", "Презентация");
         else if (data == "UnUnCategoryInfographica") AddCategory(message, "https://kwork.ru/projects?c=270&attr=314408", "Инфографика");
         else if (data == "UnUnCategoryMaphSchemes") AddCategory(message, "https://kwork.ru/projects?c=270&attr=314418", "Карта и схема");
         else if (data == "UnUnCategoryVector") AddCategory(message, "https://kwork.ru/projects?c=68&attr=399848", "Отрисовка в векторе");
         else if (data == "UnUnCategory3D") AddCategory(message, "https://kwork.ru/projects?c=68&attr=399851", "3D графика");
         else if (data == "UnUnCategoryPhotomotage") AddCategory(message, "https://kwork.ru/projects?c=68&attr=399860", "Фотомонтаж и обработка");
         else if (data == "UnUnCategoryBuildboard") AddCategory(message, "https://kwork.ru/projects?c=272&attr=456459", "Билборды и стенды");
         else if (data == "UnUnCategoryVitrins") AddCategory(message, "https://kwork.ru/projects?c=272&attr=456460", "Витрины и вывески");
         else if (data == "UnUnCategorySocDesign") AddCategory(message, "https://kwork.ru/projects?c=286&attr=1433356", "Дизайн в соцсетях");
         else if (data == "UnUnCategoryMarketplaceDesign") AddCategory(message, "https://kwork.ru/projects?c=286&attr=1433413", "Дизайн для маркетплейсов");
         #endregion
         #region devIT
         else if (data == "UnCategoryDevSite") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Доработка сайта", "UnUnCategoryAfterDev") }, new[] { InlineKeyboardButton.WithCallbackData("Настройка сайта", "UnUnCategorySettingSite") }, new[] { InlineKeyboardButton.WithCallbackData("Защита и лечение сайта", "UnUnCategoryProtectSite") }, new[] { InlineKeyboardButton.WithCallbackData("Ускорение сайта", "UnUnCategorySpeedSite") }, new[] { InlineKeyboardButton.WithCallbackData("Плагины и темы", "UnUnCategoryPlugin") }, new[] { InlineKeyboardButton.WithCallbackData("Исправление ошибок", "UnUnCategoryCorrectBugs") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Доработка и настройка сайта\"", "UnUnCategoryAllDevSite") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategorySite") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Новый сайт", "UnUnCategoryNewSite") }, new[] { InlineKeyboardButton.WithCallbackData("Копия сайта", "UnUnCategoryCopySite") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Создание сайта\"", "UnUnCategoryAllSite") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryScript") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Парсеры", "UnUnCategoryParsing") }, new[] { InlineKeyboardButton.WithCallbackData("Чат-боты", "UnUnCategoryBots") }, new[] { InlineKeyboardButton.WithCallbackData("Скрипты", "UnUnCategoryScripts") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Скрипты и боты\"", "UnUnCategoryAllScript") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryVerstka") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Верстка по макету", "UnUnCategoryVerMaket") }, new[] { InlineKeyboardButton.WithCallbackData("Доработка и адаптация верстки", "UnUnCategoryAdaptationVer") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Верстка\"", "UnUnCategoryAllVerstka") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryDesk") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Макросы для Office", "UnUnCategoryMacrosOffice") }, new[] { InlineKeyboardButton.WithCallbackData("1С", "UnUnCategory1С") }, new[] { InlineKeyboardButton.WithCallbackData("Программы на заказ", "UnUnCategoryOrderApp") }, new[] { InlineKeyboardButton.WithCallbackData("Готовые программы", "UnUnCategoryCompleteApp") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Десктоп программирование\"", "UnUnCategoryAllDesk") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryMobile") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Android", "UnUnCategoryAndroid") }, new[] { InlineKeyboardButton.WithCallbackData("IOS", "UnUnCategoryIOS") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Мобильные приложения\"", "UnUnCategoryAllMobile") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryGames") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Разработка игр", "UnUnCategoryDevGame") }, new[] { InlineKeyboardButton.WithCallbackData("Готовые игры", "UnUnCategoryCompleteGame") }, new[] { InlineKeyboardButton.WithCallbackData("Игровой сервер", "UnUnCategoryGameServer") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Игры\"", "UnUnCategoryAllGames") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryServers") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Администрирование сервера", "UnUnCategoryAdmServer") }, new[] { InlineKeyboardButton.WithCallbackData("Домены", "UnUnCategoryDomains") }, new[] { InlineKeyboardButton.WithCallbackData("Хостинг", "UnUnCategoryHosting") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Сервера и хостинг\"", "UnUnCategoryAllServer") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryUse") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Юзабилити-аудит", "UnUnCategoryUseAudit") }, new[] { InlineKeyboardButton.WithCallbackData("Тестирование на ошибки", "UnUnCategoryTestBugs") }, new[] { InlineKeyboardButton.WithCallbackData("Компьютерерная и IT помощь", "UnUnCategoryPCHelp") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Юзабилити, тесты и помощь\"", "UnUnCategoryAllUse") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryDevIT") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryAllDevIT") AddCategory(message, "https://kwork.ru/projects?view=0&c=11", "Разработка и IT");
         else if (data == "UnUnCategoryAllDevSite") AddCategory(message, "https://kwork.ru/projects?view=0&c=38", "Доработка и настройка сайта");
         else if (data == "UnUnCategoryAllSite") AddCategory(message, "https://kwork.ru/projects?view=0&c=37", "Создание сайта");
         else if (data == "UnUnCategoryAllScript") AddCategory(message, "https://kwork.ru/projects?view=0&c=41", "Скрипты и боты");
         else if (data == "UnUnCategoryAllVerstka") AddCategory(message, "https://kwork.ru/projects?view=0&c=79", "Верстка");
         else if (data == "UnUnCategoryAllDesk") AddCategory(message, "https://kwork.ru/projects?view=0&c=80", "Десктоп программирование");
         else if (data == "UnUnCategoryAllMobile") AddCategory(message, "https://kwork.ru/projects?view=0&c=39", "Мобильные приложения");
         else if (data == "UnUnCategoryAllGames") AddCategory(message, "https://kwork.ru/projects?view=0&c=40", "Игры");
         else if (data == "UnCategoryAllServer") AddCategory(message, "https://kwork.ru/projects?view=0&c=255", "Сервера и хостинг");
         else if (data == "UnUnCategoryAllUse") AddCategory(message, "https://kwork.ru/projects?view=0&c=81", "Юзабилити, тесты и помощь");
         else if (data == "UnUnCategoryAfterDev") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1271", "Доработка сайта");
         else if (data == "UnUnCategorySettingSite") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1275", "Настройка сайта");
         else if (data == "UnUnCategoryProtectSite") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1273", "Защита и лечение сайта");
         else if (data == "UnUnCategorySpeedSite") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1587", "Ускорение сайта");
         else if (data == "UnUnCategoryPlugin") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1276", "Плагины и темы");
         else if (data == "UnUnCategoryCorrectBugs") AddCategory(message, "https://kwork.ru/projects?c=38&attr=1272", "Исправление ошибок");
         else if (data == "UnUnCategoryParsing") AddCategory(message, "https://kwork.ru/projects?c=41&attr=211", "Парсеры");
         else if (data == "UnUnCategoryBots") AddCategory(message, "https://kwork.ru/projects?c=41&attr=3587", "Чат-боты");
         else if (data == "UnUnCategoryScripts") AddCategory(message, "https://kwork.ru/projects?c=41&attr=7352", "Скрипты");
         else if (data == "UnUnCategoryNewSite") AddCategory(message, "https://kwork.ru/projects?c=37&attr=5016", "Новый сайт");
         else if (data == "UnUnCategoryCopySite") AddCategory(message, "https://kwork.ru/projects?c=37&attr=5017", "Копия сайта");
         else if (data == "UnUnCategoryVerMaket") AddCategory(message, "https://kwork.ru/projects?c=79&attr=224", "Верстка по макету");
         else if (data == "UnUnCategoryAdaptationVer") AddCategory(message, "https://kwork.ru/projects?c=79&attr=226", "Доработка и адаптация верстки");
         else if (data == "UnUnCategoryMacrosOffice") AddCategory(message, "https://kwork.ru/projects?c=80&attr=976", "Макросы для Office");
         else if (data == "UnUnCategory1С") AddCategory(message, "https://kwork.ru/projects?c=80&attr=977", "1С");
         else if (data == "UnUnCategoryOrderApp") AddCategory(message, "https://kwork.ru/projects?c=80&attr=975", "Программы на заказ");
         else if (data == "UnUnCategoryCompleteApp") AddCategory(message, "https://kwork.ru/projects?c=80&attr=1088", "Готовые программы");
         else if (data == "UnUnCategoryAndroid") AddCategory(message, "https://kwork.ru/projects?c=39&attr=1406", "Android");
         else if (data == "UnUnCategoryIOS") AddCategory(message, "https://kwork.ru/projects?c=39&attr=1405", "IOS");
         else if (data == "UnUnCategoryDevGame") AddCategory(message, "https://kwork.ru/projects?c=40&attr=7890", "Разработка игр");
         else if (data == "UnUnCategoryCompleteGame") AddCategory(message, "https://kwork.ru/projects?c=40&attr=7892", "Игровой сервер");
         else if (data == "UnUnCategoryGameServer") AddCategory(message, "https://kwork.ru/projects?c=40&attr=7891", "Готовые игры");
         else if (data == "UnUnCategoryAdmServer") AddCategory(message, "https://kwork.ru/projects?c=255&attr=471959", "Администраирование сервера");
         else if (data == "UnUnCategoryDomains") AddCategory(message, "https://kwork.ru/projects?c=255&attr=472033", "Домены");
         else if (data == "UnUnCategoryHosting") AddCategory(message, "https://kwork.ru/projects?c=255&attr=472048", "Хостинг");
         else if (data == "UnUnCategoryUseAudit") AddCategory(message, "https://kwork.ru/projects?c=81&attr=5915", "Юзабилити-аудит");
         else if (data == "UnUnCategoryTestBugs") AddCategory(message, "https://kwork.ru/projects?c=81&attr=5916", "Тестирование на ошибки");
         else if (data == "UnUnCategoryPCHelp") AddCategory(message, "https://kwork.ru/projects?c=81&attr=396954", "Компьютерная и IT помощь");
         #endregion
         #region translate
         else if (data == "AllCategoryTransText") AddCategory(message, "https://kwork.ru/projects?c=35", "Переводы");
         else if (data == "UnCategoryResume") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Текст вакансии", "UnUnCategoryTextVac") }, new[] { InlineKeyboardButton.WithCallbackData("Составление резюме", "UnUnCategoryDevResume") }, new[] { InlineKeyboardButton.WithCallbackData("Сопроводительные письма", "UnUnCategorySopMessage") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Резюме и вакансии\"", "UnUnCategoryAllResume") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryTextTranslate") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryText") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("С аудио/видео", "UnUnCategoryTextAudioVideo") }, new[] { InlineKeyboardButton.WithCallbackData("С изображений", "UnUnCategoryTextImage") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Набор текста\"", "UnUnCategoryAllText") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryTextTranslate") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryBusinessText") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Продающие и бизнес-тексты", "UnUnCategorySellBusinessText") }, new[] { InlineKeyboardButton.WithCallbackData("Реклама и email", "UnUnCategoryResumeAdEmail") }, new[] { InlineKeyboardButton.WithCallbackData("Скрипты продаж и выступлений", "UnUnCategoryResumeScriptsSell") }, new[] { InlineKeyboardButton.WithCallbackData("Коммерческие предложения", "UnUnCategoryResumeComerical") }, new[] { InlineKeyboardButton.WithCallbackData("Посты для соцсетей", "UnUnCategoryPostSoc") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Продающие и бизнес-тексты\"", "UnUnCategoryAllBusinessText") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryTextTranslate") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryTextSite") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Статьи", "UnUnCategoryStates") }, new[] { InlineKeyboardButton.WithCallbackData("SEO-тексты", "UnUnCategorySEOText") }, new[] { InlineKeyboardButton.WithCallbackData("Карточки товаров", "UnUnCategoryProductCard") }, new[] { InlineKeyboardButton.WithCallbackData("Комментарии", "UnUnCategoryComment") }, new[] { InlineKeyboardButton.WithCallbackData("Художественные тексты", "UnUnCategoryHudText") }, new[] { InlineKeyboardButton.WithCallbackData("Сценарии", "UnUnCategoryScena") }, new[] { InlineKeyboardButton.WithCallbackData("Корректура", "UnUnCategoryCorrect") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Тексты и наполнение сайта\"", "UnUnCategoryAllTextSite") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryTextTranslate") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryTranslate") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("С текста", "UnUnCategoryTransText") }, new[] { InlineKeyboardButton.WithCallbackData("С аудио/видео", "UnUnCategoryTransAudioVideo") }, new[] { InlineKeyboardButton.WithCallbackData("С изображения", "UnUnCategoryTransImage") }, new[] { InlineKeyboardButton.WithCallbackData("Переводы устные", "UnUnCategoryTransListen") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Переводы\"", "UnUnCategoryAllTranslate") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryTextTranslate") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnUnCategoryAllResume") AddCategory(message, "https://kwork.ru/projects?view=0&c=235", "Резюме и вакансии");
         else if (data == "UnUnCategoryAllText") AddCategory(message, "https://kwork.ru/projects?view=0&c=75", "Набор текста");
         else if (data == "UnUnCategoryAllBusinessText") AddCategory(message, "https://kwork.ru/projects?view=0&c=74", "Продающие и бизнес-тексты");
         else if (data == "UnUnCategoryAllTextSite") AddCategory(message, "https://kwork.ru/projects?view=0&c=73", "Тексты и наполнение сайта");
         else if (data == "UnUnCategoryAllTranslate") AddCategory(message, "https://kwork.ru/projects?view=0&c=35", "Переводы");
         else if (data == "UnUnCategoryTextVac") AddCategory(message, "https://kwork.ru/projects?c=235&attr=3325", "Текст вакансии");
         else if (data == "UnUnCategoryDevResume") AddCategory(message, "https://kwork.ru/projects?c=235&attr=3326", "Составление резюме");
         else if (data == "UnUnCategorySopMessage") AddCategory(message, "https://kwork.ru/projects?c=235&attr=202220", "Сопроводительные письма");
         else if (data == "UnUnCategoryTextAudioVideo") AddCategory(message, "https://kwork.ru/projects?c=75&attr=1187", "Набор текста с аудио/видео");
         else if (data == "UnUnCategoryTextImage") AddCategory(message, "https://kwork.ru/projects?c=75&attr=1188", "Набор текста с изображений");
         else if (data == "UnUnCategorySellBusinessText") AddCategory(message, "https://kwork.ru/projects?c=74&attr=236", "Продающие тексты");
         else if (data == "UnUnCategoryResumeAdEmail") AddCategory(message, "https://kwork.ru/projects?c=74&attr=240", "Реклама и email");
         else if (data == "UnUnCategoryResumeScriptsSell") AddCategory(message, "https://kwork.ru/projects?c=74&attr=404288", "Скрипты продаж и выступлений");
         else if (data == "UnUnCategoryResumeComerical") AddCategory(message, "https://kwork.ru/projects?c=74&attr=404242", "Коммерческие предложения");
         else if (data == "UnUnCategoryPostSoc") AddCategory(message, "https://kwork.ru/projects?c=74&attr=452907", "Посты для соцсетей");
         else if (data == "UnUnCategoryStates") AddCategory(message, "https://kwork.ru/projects?c=73&attr=483484", "Статьи");
         else if (data == "UnUnCategorySEOText") AddCategory(message, "https://kwork.ru/projects?c=73&attr=478352", "SEO-тексты");
         else if (data == "UnUnCategoryProductCard") AddCategory(message, "https://kwork.ru/projects?c=73&attr=478418", "Карточки товаров");
         else if (data == "UnUnCategoryComment") AddCategory(message, "https://kwork.ru/projects?c=73&attr=456587", "Комментарии");
         else if (data == "UnUnCategoryHudText") AddCategory(message, "https://kwork.ru/projects?c=73&attr=456533", "Художественные тексты");
         else if (data == "UnUnCategoryScena") AddCategory(message, "https://kwork.ru/projects?c=73&attr=456546", "Сценарии");
         else if (data == "UnUnCategoryCorrect") AddCategory(message, "https://kwork.ru/projects?c=73&attr=456594", "Корректура текстов");
         else if (data == "UnUnCategoryTransText") AddCategory(message, "https://kwork.ru/projects?c=35&attr=6413", "Перевод текста");
         else if (data == "UnUnCategoryTransAudioVideo") AddCategory(message, "https://kwork.ru/projects?c=35&attr=6412", "Перевод текста с аудио/видео");
         else if (data == "UnUnCategoryTransImage") AddCategory(message, "https://kwork.ru/projects?c=35&attr=6414", "Перевод текста с изображения");
         else if (data == "UnUnCategoryTransListen") AddCategory(message, "https://kwork.ru/projects?c=35&attr=6415", "Устные переводы");
         #endregion
         #region SEO
         else if (data == "UnCategoryTrafic") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Посетители на сайт", "UnUnCategoryPeopleSite") }, new[] { InlineKeyboardButton.WithCallbackData("Поведенческие факторы", "UnUnCategoryPovFactor") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Трафик\"", "UnUnCategoryAllTrafic") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryThread") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("С нуля", "UnUnCategoryTextThreadNull") }, new[] { InlineKeyboardButton.WithCallbackData("По сайту", "UnUnCategoryThreadOnSite") }, new[] { InlineKeyboardButton.WithCallbackData("Готовое ядро", "UnUnCategoryCompleteThread") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Семантическое ядро\"", "UnUnCategoryAllThread") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategorySources") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Статейные и крауд", "UnUnCategoryKraud") }, new[] { InlineKeyboardButton.WithCallbackData("Форумные", "UnUnCategoryForums") }, new[] { InlineKeyboardButton.WithCallbackData("Каталоги сайтов", "UnUnCategoryCatalogSite") }, new[] { InlineKeyboardButton.WithCallbackData("В комментариях", "UnUnCategoryInComment") }, new[] { InlineKeyboardButton.WithCallbackData("В соцсетях", "UnUnCategoryOnSoc") }, new[] { InlineKeyboardButton.WithCallbackData("В профилях", "UnUnCategoryOnProfile") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Ссылки\"", "UnUnCategoryAllSources") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnCategoryAnaletic") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Метрики и счетчики", "UnUnCategoryMetric") }, new[] { InlineKeyboardButton.WithCallbackData("Анализ сайтов, рынка", "UnUnCategoryAnalysMarket") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Сататистика и аналитика\"", "UnUnCategoryAllAnalytic") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);

         }
         else if (data == "UnCategorySEOAudit") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("SEO аудит", "UnUnCategorySEOAudit") }, new[] { InlineKeyboardButton.WithCallbackData("Консультация", "UnUnCategorySEOConsultation") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"SEO аудиты, консультации\"", "UnUnCategoryAllSEOAudit") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);

         }
         else if (data == "UnCategoryOptimization") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Полная оптимизация", "UnUnCategoryFullOpt") }, new[] { InlineKeyboardButton.WithCallbackData("Оптимизация страниц", "UnUnCategoryOptPages") }, new[] { InlineKeyboardButton.WithCallbackData("Robots и sitemap", "UnUnCategoryRobotsSitemap") }, new[] { InlineKeyboardButton.WithCallbackData("Теги", "UnUnCategoryTags") }, new[] { InlineKeyboardButton.WithCallbackData("Микроразметка", "UnUnCategoryMicroRazm") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Внутренняя оптимизация\"", "UnUnCategoryAllOptimization") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategorySEOTraficc") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "🛒 Выберите категорию", replyMarkup: keyborad);
         }
         else if (data == "UnUnCategoryAllTrafic") AddCategory(message, "https://kwork.ru/projects?view=0&c=72", "Трафик");
         else if (data == "UnUnCategoryAllThread") AddCategory(message, "https://kwork.ru/projects?view=0&c=71", "Семантическое ядро");
         else if (data == "UnUnCategoryAllSources") AddCategory(message, "https://kwork.ru/projects?view=0&c=59", "Ссылки");
         else if (data == "UnUnCategoryAllAnalytic") AddCategory(message, "https://kwork.ru/projects?view=0&c=56", "Статистика и аналитика");
         else if (data == "UnUnCategoryAllSEOAudit") AddCategory(message, "https://kwork.ru/projects?view=0&c=44", "SEO аудиты, консультации");
         else if (data == "UnUnCategoryAllOptimization") AddCategory(message, "https://kwork.ru/projects?view=0&c=43", "Внутренняя оптимизация");
         else if (data == "UnCategoryTopSite") AddCategory(message, "https://kwork.ru/projects?c=273", "Продвижение сайта в топ");
         else if (data == "UnCategoryAllSEOTrafic") AddCategory(message, "https://kwork.ru/projects?c=17", "SEO и трафик");
         else if (data == "UnUnCategoryPeopleSite") AddCategory(message, "https://kwork.ru/projects?c=72&attr=199", "Посетители на сайт");
         else if (data == "UnUnCategoryPovFactor") AddCategory(message, "https://kwork.ru/projects?c=72&attr=200", "Поведенческие факторы");
         else if (data == "UnUnCategoryTextThreadNull") AddCategory(message, "https://kwork.ru/projects?c=71&attr=3799", "Семантическое ядро с нуля");
         else if (data == "UnUnCategoryThreadOnSite") AddCategory(message, "https://kwork.ru/projects?c=71&attr=3800", "Семантическое ядро по сайту");
         else if (data == "UnUnCategoryCompleteThread") AddCategory(message, "https://kwork.ru/projects?c=71&attr=186454", "Готовое семантическое ядро");
         else if (data == "UnUnCategoryKraud") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1379472", "Статейные и крауд ссылки");
         else if (data == "UnUnCategoryForums") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1379027", "Формуные ссылки");
         else if (data == "UnUnCategoryCatalogSite") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1379024", "Каталоги сайтов");
         else if (data == "UnUnCategoryInComment") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1379022", "Ссылки в комментариях");
         else if (data == "UnUnCategoryOnSoc") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1378934", "Ссылки в соцсетях");
         else if (data == "UnUnCategoryOnProfile") AddCategory(message, "https://kwork.ru/projects?c=59&attr=1378848", "Ссылки в профилях");
         else if (data == "UnUnCategoryMetric") AddCategory(message, "https://kwork.ru/projects?c=56&attr=942", "Метрики и счетчики");
         else if (data == "UnUnCategoryAnalysMarket") AddCategory(message, "https://kwork.ru/projects?c=56&attr=943", "Анализ сайтов, рынка");
         else if (data == "UnUnCategorySEOAudit") AddCategory(message, "https://kwork.ru/projects?c=44&attr=1120", "SEO аудит");
         else if (data == "UnUnCategorySEOConsultation") AddCategory(message, "https://kwork.ru/projects?c=44&attr=1121", "SEO консультации");
         else if (data == "UnUnCategoryFullOpt") AddCategory(message, "https://kwork.ru/projects?c=43&attr=478713", "Полная оптимизация");
         else if (data == "UnUnCategoryOptPages") AddCategory(message, "https://kwork.ru/projects?c=43&attr=478714", "Оптимизация страниц");
         else if (data == "UnUnCategoryRobotsSitemap") AddCategory(message, "https://kwork.ru/projects?c=43&attr=478716", "Robots и sitemap");
         else if (data == "UnUnCategoryTags") AddCategory(message, "https://kwork.ru/projects?c=43&attr=478717", "Теги");
         else if (data == "UnUnCategoryMicroRazm") AddCategory(message, "https://kwork.ru/projects?c=43&attr=478720", "Микроразметка");
         #endregion
         #region socandadvert
         else if (data == "UnCategoryDBUsers") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Сбор данных", "UnUnCategoryTakeData") }, new[] { InlineKeyboardButton.WithCallbackData("Готовые базы данных", "UnUnCategoryCompleteDB") }, new[] { InlineKeyboardButton.WithCallbackData("Проверка, чистка базы", "UnUnCategoryCheckDB") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Базы данных и клиентов\"", "UnUnCategoryAllDBUsers") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryMarketAds") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Маркетплейсы", "UnUnCategoryMarketplaces") }, new[] { InlineKeyboardButton.WithCallbackData("Доски объявлений", "UnUnCategoryAdBoard") }, new[] { InlineKeyboardButton.WithCallbackData("Справочники и каталоги", "UnUnCategoryCatalogs") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Маркетплейсы и доски объявлений\"", "UnUnCategoryAllMarketAds") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryEMailMarketing") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Отправка рассылки", "UnUnCategorySendRass") }, new[] { InlineKeyboardButton.WithCallbackData("Почтовые ящики", "UnUnCategoryEmails") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"E-mail маркетинг и рассылки\"", "UnUnCategoryAllEmailMarketing") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryContextAdvert") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Яндекс Директ", "UnUnCategoryYaDirect") }, new[] { InlineKeyboardButton.WithCallbackData("Google Ads", "UnUnCategoryGoogleAds") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Контекстная реклама\"", "UnUnCategoryAllContextAds") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryAdvertPR") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Размещение рекламы", "UnUnCategorySendAdvert") }, new[] { InlineKeyboardButton.WithCallbackData("Контент-маркетинг", "UnUnCategoryContentMarketing") }, new[] { InlineKeyboardButton.WithCallbackData("Продвижение музыки", "UnUnCategoryUpMusic") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Реклама PR\"", "UnUnCategoryAllAdvertPR") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategorySMM") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Instagram", "UnUnCategoryInst") }, new[] { InlineKeyboardButton.WithCallbackData("YouTube", "UnUnCategoryYouTube") }, new[] { InlineKeyboardButton.WithCallbackData("ВКонтакте", "UnUnCategoryVK") }, new[] { InlineKeyboardButton.WithCallbackData("Facebook", "UnUnCategoryFacebook") }, new[] { InlineKeyboardButton.WithCallbackData("Telegram", "UnUnCategoryTelegram") }, new[] { InlineKeyboardButton.WithCallbackData("Одноклассники", "UnUnCategoryOK") }, new[] { InlineKeyboardButton.WithCallbackData("Яндекс Дзен", "UnUnCategoryDzen") }, new[] { InlineKeyboardButton.WithCallbackData("Twitter", "UnUnCategoryTwitter") }, new[] { InlineKeyboardButton.WithCallbackData("Другие", "UnUnCategoryOtherSoc") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Соцсети и SMM\"", "UnUnCategoryAllSMM") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryOnlineAds") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnUnCategoryAllDBUsers") AddCategory(message, "https://kwork.ru/projects?view=0&c=113", "Базы данных и клиентов");
         else if (data == "UnUnCategoryAllMarketAds") AddCategory(message, "https://kwork.ru/projects?view=0&c=112", "Маркетплейсы и доски объявлений");
         else if (data == "UnUnCategoryAllEmailMarketing") AddCategory(message, "https://kwork.ru/projects?view=0&c=108", "E-mail маркетинг и рассылки");
         else if (data == "UnUnCategoryAllContextAds") AddCategory(message, "https://kwork.ru/projects?view=0&c=49", "Контекстная реклама");
         else if (data == "UnUnCategoryAllAdvertPR") AddCategory(message, "https://kwork.ru/projects?view=0&c=47", "Реклама и PR");
         else if (data == "UnUnCategoryAllSMM") AddCategory(message, "https://kwork.ru/projects?view=0&c=46", "Соцсети и SMM");
         else if (data == "UnCategoryAllOnlineAds") AddCategory(message, "https://kwork.ru/projects?c=45", "Соцсети и реклама");
         else if (data == "UnUnCategoryTakeData") AddCategory(message, "https://kwork.ru/projects?c=113&attr=1116", "Сбор данных БД");
         else if (data == "UnUnCategoryCompleteDB") AddCategory(message, "https://kwork.ru/projects?c=113&attr=1117", "Готовые БД");
         else if (data == "UnUnCategoryCheckDB") AddCategory(message, "https://kwork.ru/projects?c=113&attr=1118", "Проверка, чистка БД");
         else if (data == "UnUnCategoryMarketplaces") AddCategory(message, "https://kwork.ru/projects?c=112&attr=1357", "Маркетплейсы");
         else if (data == "UnUnCategoryAdBoard") AddCategory(message, "https://kwork.ru/projects?c=112&attr=1466231", "Доски объявлений");
         else if (data == "UnUnCategoryCatalogs") AddCategory(message, "https://kwork.ru/projects?c=112&attr=233", "Справочники и каталоги");
         else if (data == "UnUnCategorySendRass") AddCategory(message, "https://kwork.ru/projects?c=108&attr=938", "Отправка рассылки");
         else if (data == "UnUnCategoryEmails") AddCategory(message, "https://kwork.ru/projects?c=108&attr=939", "Почтовые ящики");
         else if (data == "UnUnCategoryYaDirect") AddCategory(message, "https://kwork.ru/projects?c=49&attr=206", "Контекстная реклама Яндекс Директ");
         else if (data == "UnUnCategoryGoogleAds") AddCategory(message, "https://kwork.ru/projects?c=49&attr=207", "Контекстная реклама Google Ads");
         else if (data == "UnUnCategorySendAdvert") AddCategory(message, "https://kwork.ru/projects?c=47&attr=932", "Размещение рекламы");
         else if (data == "UnUnCategoryContentMarketing") AddCategory(message, "https://kwork.ru/projects?c=47&attr=1356", "Контент-маркетинг");
         else if (data == "UnUnCategoryUpMusic") AddCategory(message, "https://kwork.ru/projects?c=47&attr=710463", "Продвижение музыки");
         else if (data == "UnUnCategoryInst") AddCategory(message, "https://kwork.ru/projects?c=46&attr=258", "SMM Instagram");
         else if (data == "UnUnCategoryYouTube") AddCategory(message, "https://kwork.ru/projects?c=46&attr=265", "SMM Youtube");
         else if (data == "UnUnCategoryVK") AddCategory(message, "https://kwork.ru/projects?c=46&attr=242", "SMM ВКонтакте");
         else if (data == "UnUnCategoryFacebook") AddCategory(message, "https://kwork.ru/projects?c=46&attr=250", "SMM Facebook");
         else if (data == "UnUnCategoryTelegram") AddCategory(message, "https://kwork.ru/projects?c=46&attr=281", "SMM Telegram");
         else if (data == "UnUnCategoryOK") AddCategory(message, "https://kwork.ru/projects?c=46&attr=273", "SMM Одноклассники");
         else if (data == "UnUnCategoryDzen") AddCategory(message, "https://kwork.ru/projects?c=46&attr=7912", "Яндекс Дзен");
         else if (data == "UnUnCategoryTwitter") AddCategory(message, "https://kwork.ru/projects?c=46&attr=287", "SMM Twiter");
         else if (data == "UnUnCategoryOtherSoc") AddCategory(message, "https://kwork.ru/projects?c=46&attr=302", "SMM другие соцсети");
         #endregion
         #region audioozv
         else if (data == "UnCategoryEditAudio") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Обработка звука", "UnUnCategoryTreatSong") }, new[] { InlineKeyboardButton.WithCallbackData("Выделение звука из видео", "UnUnCategoryCutSoundVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Редактирование аудио\"", "UnUnCategoryAllEditAudio") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryVideomotage") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Видеосъемка", "UnUnCategoryVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Монтаж и обработка видео", "UnUnCategoryMontage") }, new[] { InlineKeyboardButton.WithCallbackData("Фотосъемка", "UnUnCategoryPhoto") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Видеосъемка и монтаж\"", "UnUnCategoryAllVideomontage") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryIntroLogo") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Анимация логотипа", "UnUnCategoryAnimateLogo") }, new[] { InlineKeyboardButton.WithCallbackData("Интро и заставки", "UnUnCategoryIntroZas") }, new[] { InlineKeyboardButton.WithCallbackData("GIF-анимация", "UnUnCategoryGIF") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Интро и анимация логотипа\"", "UnUnCategoryAllAnimationLogo") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryVideo") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Дудл-видео", "UnUnCategoryDoodleVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Анимационный ролик", "UnUnCategoryAnimVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Проморолик", "UnUnCategoryPromoVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Ролики для соцсетей", "UnUnCategorySocVideo") }, new[] { InlineKeyboardButton.WithCallbackData("3D анимация", "UnUnCategory3DAnimation") }, new[] { InlineKeyboardButton.WithCallbackData("Слайд-шоу", "UnUnCategorySlideShow") }, new[] { InlineKeyboardButton.WithCallbackData("Скринкасты и видеообзоры", "UnUnCategoryScreencast") }, new[] { InlineKeyboardButton.WithCallbackData("Кинетическая типографика", "UnUnCategoryTypogprapghic") }, new[] { InlineKeyboardButton.WithCallbackData("Видео с ведущим", "UnUnCategoryVedyshVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Видеопрезентация", "UnUnCategoryVideoPresent") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Видеоролики\"", "UnUnCategoryAllVideo") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryMusicSong") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Написание музыки", "UnUnCategoryCreateSong") }, new[] { InlineKeyboardButton.WithCallbackData("Тексты песен", "UnUnCategorySongText") }, new[] { InlineKeyboardButton.WithCallbackData("Аранжировка", "UnUnCategoryArang") }, new[] { InlineKeyboardButton.WithCallbackData("Запись вокала", "UnUnCategoryRecordVocal") }, new[] { InlineKeyboardButton.WithCallbackData("Песня (музыка + текст + вокал)", "UnUnCategoryFullSong") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Музыка и песни\"", "UnUnCategoryAllMusicSong") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryOzv") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Озвучка и дикторы", "UnUnCategoryOzvDictor") }, new[] { InlineKeyboardButton.WithCallbackData("Аудиоролик", "UnUnCategoryAudioroll") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Аудиозапись и озвучка\"", "UnUnCategoryAllOzv") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryAudioVideo") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnUnCategoryAllEditAudio") AddCategory(message, "https://kwork.ru/projects?view=0&c=106", "Редактирование аудио");
         else if (data == "UnUnCategoryAllVideomontage") AddCategory(message, "https://kwork.ru/projects?view=0&c=78", "Видеосъемка и монтаж");
         else if (data == "UnUnCategoryAllAnimationLogo") AddCategory(message, "https://kwork.ru/projects?view=0&c=77", "Интро и анимация логотипы");
         else if (data == "UnUnCategoryAllVideo") AddCategory(message, "https://kwork.ru/projects?view=0&c=76", "Видеоролики");
         else if (data == "UnUnCategoryAllMusicSong") AddCategory(message, "https://kwork.ru/projects?view=0&c=23", "Музыка и песни");
         else if (data == "UnCategoryAllAudioVideo") AddCategory(message, "https://kwork.ru/projects?c=7", "Аудио, видео, съемка");
         else if (data == "UnUnCategoryAllOzv") AddCategory(message, "https://kwork.ru/projects?view=0&c=20", "Аудиозапись и озвучка");
         else if (data == "UnUnCategoryTreatSong") AddCategory(message, "https://kwork.ru/projects?c=106&attr=1226", "Обработка звука");
         else if (data == "UnUnCategoryCutSoundVideo") AddCategory(message, "https://kwork.ru/projects?c=106&attr=1227", "Выделение звука из видео");
         else if (data == "UnUnCategoryVideo") AddCategory(message, "https://kwork.ru/projects?c=78&attr=4644", "Видеосъемка");
         else if (data == "UnUnCategoryMontage") AddCategory(message, "https://kwork.ru/projects?c=78&attr=4645", "Монтаж и обработка видео");
         else if (data == "UnUnCategoryPhoto") AddCategory(message, "https://kwork.ru/projects?c=78&attr=322707", "Фотосъемка");
         else if (data == "UnUnCategoryAnimateLogo") AddCategory(message, "https://kwork.ru/projects?c=77&attr=210161", "Анимация логотипа");
         else if (data == "UnUnCategoryIntroZas") AddCategory(message, "https://kwork.ru/projects?c=77&attr=210163", "Интро и заставки");
         else if (data == "UnUnCategoryGIF") AddCategory(message, "https://kwork.ru/projects?c=77&attr=311032", "GIF-анимация");
         else if (data == "UnUnCategoryDoodleVideo") AddCategory(message, "https://kwork.ru/projects?c=76&attr=831", "Дудл-видео");
         else if (data == "UnUnCategoryAnimVideo") AddCategory(message, "https://kwork.ru/projects?c=76&attr=832", "Анимационный ролик");
         else if (data == "UnUnCategoryPromoVideo") AddCategory(message, "https://kwork.ru/projects?c=76&attr=833", "Проморолик");
         else if (data == "UnUnCategorySocVideo") AddCategory(message, "https://kwork.ru/projects?c=76&attr=735162", "Ролики для соцсетей");
         else if (data == "UnUnCategory3DAnimation") AddCategory(message, "https://kwork.ru/projects?c=76&attr=834", "3D анимация");
         else if (data == "UnUnCategorySlideShow") AddCategory(message, "https://kwork.ru/projects?c=76&attr=6300", "Слайд-шоу");
         else if (data == "UnUnCategoryScreencast") AddCategory(message, "https://kwork.ru/projects?c=76&attr=2070", "Скринкасты и видеобзоры");
         else if (data == "UnUnCategoryTypogprapghic") AddCategory(message, "https://kwork.ru/projects?c=76&attr=3560", "Кинетическая типографика");
         else if (data == "UnUnCategoryVedyshVideo") AddCategory(message, "https://kwork.ru/projects?c=76&attr=211813", "Видео с ведущим");
         else if (data == "UnUnCategoryVideoPresent") AddCategory(message, "https://kwork.ru/projects?c=76&attr=314311", "Видеопрезентация");
         else if (data == "UnUnCategoryCreateSong") AddCategory(message, "https://kwork.ru/projects?c=23&attr=1221", "Написание музыки");
         else if (data == "UnUnCategorySongText") AddCategory(message, "https://kwork.ru/projects?c=23&attr=1339", "Тексты песен");
         else if (data == "UnUnCategoryArang") AddCategory(message, "https://kwork.ru/projects?c=23&attr=1224", "Аранжировка");
         else if (data == "UnUnCategoryRecordVocal") AddCategory(message, "https://kwork.ru/projects?c=23&attr=1222", "Запись вокала");
         else if (data == "UnUnCategoryFullSong") AddCategory(message, "https://kwork.ru/projects?c=23&attr=209737", "Песня (музыка + текст + вокал)");
         else if (data == "UnUnCategoryOzvDictor") AddCategory(message, "https://kwork.ru/projects?c=20&attr=1213", "Озвучка и дикторы");
         else if (data == "UnUnCategoryAudioroll") AddCategory(message, "https://kwork.ru/projects?c=20&attr=1214", "Аудиоролик");
         #endregion
         #region business
         else if (data == "UnCategorySellSite") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Сайт с доменом", "UnUnCategorySiteDomain") }, new[] { InlineKeyboardButton.WithCallbackData("Сайт без домена", "UnUnCategorySiteNoDomain") }, new[] { InlineKeyboardButton.WithCallbackData("Подбор персонала", "UnUnCategoryFindEmployee") }, new[] { InlineKeyboardButton.WithCallbackData("Соцсети, домен, приложение", "UnUnCategorySocDomApp") }, new[] { InlineKeyboardButton.WithCallbackData("Аудит, оценка, помощь", "UnUnCategoryAudHelp") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Продажа сайтов\"", "UnUnCategoryAllSellSite") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryHelper") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Поиск информации", "UnUnCategoryFindInfo") }, new[] { InlineKeyboardButton.WithCallbackData("Работа в MS Office", "UnUnCategoryWorkOffice") }, new[] { InlineKeyboardButton.WithCallbackData("Анализ информации", "UnUnCategoryAnalysInfo") }, new[] { InlineKeyboardButton.WithCallbackData("Любая интеллектуальная работа", "UnUnCategoryIntelWork") }, new[] { InlineKeyboardButton.WithCallbackData("Любая рутинная работа", "UnUnCategoryRoutineWork") }, new[] { InlineKeyboardButton.WithCallbackData("Менеджмент проектов", "UnUnCategoryManagment") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Персональный помощник\"", "UnUnCategoryAllHelper") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryBuhNalog") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Для физлиц", "UnUnCategoryFizLic") }, new[] { InlineKeyboardButton.WithCallbackData("Для юрлиц и ИП", "UnUnCategoryYrIP") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Бухгалтерия и налоги\"", "UnUnCategoryAllBuhNalog") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryYrHelp") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Договор и доверенность", "UnUnCategoryDocumentDov") }, new[] { InlineKeyboardButton.WithCallbackData("Судебный документ", "UnUnCategorySudDoc") }, new[] { InlineKeyboardButton.WithCallbackData("Ведение ООО и ИП", "UnUnCategoryOOOIP") }, new[] { InlineKeyboardButton.WithCallbackData("Юридическая консультация", "UnUnCategoryYrKons") }, new[] { InlineKeyboardButton.WithCallbackData("Интернет-право", "UnUnCategoryInternetPravo") }, new[] { InlineKeyboardButton.WithCallbackData("Визы", "UnUnCategoryVisa") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Юридическая помощь\"", "UnUnCategoryAllYrHelp") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryLesson") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Консалтинг", "UnUnCategoryKonsalting") }, new[] { InlineKeyboardButton.WithCallbackData("Онлайн курсы", "UnUnCategoryOnlineLesson") }, new[] { InlineKeyboardButton.WithCallbackData("Оформление по ГОСТу", "UnUnCategoryGOST") }, new[] { InlineKeyboardButton.WithCallbackData("Репетиторы", "UnUnCategoryRepetitor") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Обучение и консалтинг\"", "UnUnCategoryAllLesson") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryCallSell") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Продажи по телефону", "UnUnCategorySellOnPhone") }, new[] { InlineKeyboardButton.WithCallbackData("Телефонный опрос", "UnUnCategoryQuestPhone") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Обзвоны и продажи\"", "UnUnCategoryAllCallSell") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryFindEmp") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Подбор резюме", "UnUnCategoryFindResume") }, new[] { InlineKeyboardButton.WithCallbackData("Найм специалиста", "UnUnCategoryEmpSpec") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Подбор персонала\"", "UnUnCategoryAllFindEmp") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnCategoryRepair") {
            var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Строительство", "UnUnCategoryRepairs") }, new[] { InlineKeyboardButton.WithCallbackData("Проектирование объекта", "UnUnCategoryProjectObj") }, new[] { InlineKeyboardButton.WithCallbackData("Машиностроение", "UnUnCategoryCarStroy") }, new[] { InlineKeyboardButton.WithCallbackData("Предметы и аксессуары", "UnUnCategoryItemsAc") }, new[] { InlineKeyboardButton.WithCallbackData("Все подкатегории \"Стройка и ремонт\"", "UnUnCategoryAllRepair") }, new[] { InlineKeyboardButton.WithCallbackData("Назад", "CategoryBusiness") } });
            await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text, replyMarkup: keyborad);
         }
         else if (data == "UnUnCategoryAllRepair") AddCategory(message, "https://kwork.ru/projects?view=0&c=262", "Стройка и ремонт");
         else if (data == "UnUnCategoryAllFindEmp") AddCategory(message, "https://kwork.ru/projects?view=0&c=262", "Подбор персонала");
         else if (data == "UnUnCategoryAllSellSite") AddCategory(message, "https://kwork.ru/projects?view=0&c=114", "Продажа сайтов");
         else if (data == "UnUnCategoryAllHelper") AddCategory(message, "https://kwork.ru/projects?view=0&c=84", "Персональный помощник");
         else if (data == "UnUnCategoryAllBuhNalog") AddCategory(message, "https://kwork.ru/projects?view=0&c=64", "Бухгалтерия и налоги");
         else if (data == "UnUnCategoryAllYrHelp") AddCategory(message, "https://kwork.ru/projects?view=0&c=63", "Юридическая помощь");
         else if (data == "UnUnCategoryAllLesson") AddCategory(message, "https://kwork.ru/projects?view=0&c=55", "Обучение и консалтинг");
         else if (data == "UnUnCategoryAllCallSell") AddCategory(message, "https://kwork.ru/projects?view=0&c=262", "Обзвоны и продажи");
         else if (data == "UnCategoryAllBusiness") AddCategory(message, "https://kwork.ru/projects?c=83", "Бизнес и жизнь");
         else if (data == "UnUnCategorySiteDomain") AddCategory(message, "https://kwork.ru/projects?c=114&attr=470048", "Сайт с доменом");
         else if (data == "UnUnCategorySiteNoDomain") AddCategory(message, "https://kwork.ru/projects?c=114&attr=469647", "Сайт без домена");
         else if (data == "UnUnCategoryFindEmployee") AddCategory(message, "https://kwork.ru/projects?c=265", "Подбор персонала");
         else if (data == "UnUnCategorySocDomApp") AddCategory(message, "https://kwork.ru/projects?c=114&attr=470449", "Соцсети, домен, приложение");
         else if (data == "UnUnCategoryAudHelp") AddCategory(message, "https://kwork.ru/projects?c=114&attr=799926", "Аудит, оценка, помощь");
         else if (data == "UnUnCategoryFindInfo") AddCategory(message, "https://kwork.ru/projects?c=84&attr=1046", "Поиск информации");
         else if (data == "UnUnCategoryWorkOffice") AddCategory(message, "https://kwork.ru/projects?c=84&attr=1049", "Работа в MS Office");
         else if (data == "UnUnCategoryAnalysInfo") AddCategory(message, "https://kwork.ru/projects?c=84&attr=212032", "Анализ информации");
         else if (data == "UnUnCategoryIntelWork") AddCategory(message, "https://kwork.ru/projects?c=84&attr=212035", "Любая интеллектуальная работа");
         else if (data == "UnUnCategoryRoutineWork") AddCategory(message, "https://kwork.ru/projects?c=84&attr=212038", "Любая рутинная работа");
         else if (data == "UnUnCategoryManagment") AddCategory(message, "https://kwork.ru/projects?c=84&attr=396277", "Менеджмент проектов");
         else if (data == "UnUnCategoryFizLic") AddCategory(message, "https://kwork.ru/projects?c=64&attr=1058", "Бухгалтерия и налоги для физлиц");
         else if (data == "UnUnCategoryYrIP") AddCategory(message, "https://kwork.ru/projects?c=64&attr=1060", "Бухгалтерия и налоги для юрлиц и ИП");
         else if (data == "UnUnCategoryDocumentDov") AddCategory(message, "https://kwork.ru/projects?c=63&attr=1052", "Договор и доверенность");
         else if (data == "UnUnCategorySudDoc") AddCategory(message, "https://kwork.ru/projects?c=63&attr=1053", "Судебный документ");
         else if (data == "UnUnCategoryOOOIP") AddCategory(message, "https://kwork.ru/projects?c=63&attr=1055", "Ведение ООО и ИП");
         else if (data == "UnUnCategoryYrKons") AddCategory(message, "https://kwork.ru/projects?c=63&attr=1054", "Юридическая консультация");
         else if (data == "UnUnCategoryInternetPravo") AddCategory(message, "https://kwork.ru/projects?c=63&attr=1056", "Интернет-право");
         else if (data == "UnUnCategoryVisa") AddCategory(message, "https://kwork.ru/projects?c=63&attr=313439", "Визы");
         else if (data == "UnUnCategoryKonsalting") AddCategory(message, "https://kwork.ru/projects?c=55&attr=313903", "Консалтинг");
         else if (data == "UnUnCategoryOnlineLesson") AddCategory(message, "https://kwork.ru/projects?c=55&attr=1210", "Онлайн курсы обучения и консалтинга");
         else if (data == "UnUnCategoryGOST") AddCategory(message, "https://kwork.ru/projects?c=55&attr=313914", "Оформление по ГОСТу");
         else if (data == "UnUnCategoryRepetitor") AddCategory(message, "https://kwork.ru/projects?c=55&attr=313915", "Репетиторы консалтинга");
         else if (data == "UnUnCategorySellOnPhone") AddCategory(message, "https://kwork.ru/projects?c=262&attr=312334", "Продажи по телефону");
         else if (data == "UnUnCategoryQuestPhone") AddCategory(message, "https://kwork.ru/projects?c=262&attr=312337", "Телефонный опрос");
         else if (data == "UnUnCategoryFindResume") AddCategory(message, "https://kwork.ru/projects?c=265&attr=312496", "Подбор резюме");
         else if (data == "UnUnCategoryEmpSpec") AddCategory(message, "https://kwork.ru/projects?c=265&attr=312503", "Найм специалиста");
         else if (data == "UnUnCategoryRepairs") AddCategory(message, "https://kwork.ru/projects?c=65&attr=212246", "Строительство");
         else if (data == "UnUnCategoryProjectObj") AddCategory(message, "https://kwork.ru/projects?c=65&attr=212284", "Проектирование объекта");
         else if (data == "UnUnCategoryCarStroy") AddCategory(message, "https://kwork.ru/projects?c=65&attr=212336", "Машиностроение");
         else if (data == "UnUnCategoryItemsAc") AddCategory(message, "https://kwork.ru/projects?c=65&attr=219572", "Предметы и аксессуары");
         #endregion
      }

      private static async void AddCategory(Telegram.Bot.Types.Message message, string source, string title)
      {
         try {
            ConnectDB.LoadData(users);
            string sub = users.Find(item => item.user_id == message.Chat.Id.ToString()).sub;
            if (sub != "none") {
               string subCheck = users.Find(item => item.user_id == message.Chat.Id.ToString()).sub;
               ConnectDB.LoadData(users);
               string check = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories;
               string[] category = new string[0];
               if (check != null && check != "" && check != "none") {
                  if (check.Contains("|")) {
                     string[] temp = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories.Split('|');
                     category = new string[temp.Length];
                     category = temp;
                  }
                  else {
                     category = new string[1];
                     category[0] = users.Find(item => item.user_id == message.Chat.Id.ToString()).categories;
                  }
               }
               if (category.Length < 5) {
                  string request = string.Empty;
                  for (int i = 0; i < category.Length; i++) {
                     request += category[i] + "|";
                     if (category[i].Split('*')[0] == title) { var ms = await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Категория уже добавлена"); TimerMessage(ms); return; }
                  }
                  request += title + "‼" + source;
                  ConnectDB.Query("update User set categories = '" + request + "' where user_id = " + message.Chat.Id.ToString() + ";");
                  var msg = await client.SendTextMessageAsync(message.Chat.Id, "✅ Категория \"" + title + "\" успешно добавлена");
                  TimerMessage(msg);
               }
               else await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Можно добавить не более 5-ти категорий");
            }
            else {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Оформить подписку", "GoSub") } });
               await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Приобретите подписку чтобы пользоваться ботом", replyMarkup: keyborad);
            }
         } catch { var msg = await client.SendTextMessageAsync(message.Chat.Id, "⛔️ Не удалось добавить категорию"); TimerMessage(msg); }

      }

      private static async void ParseUser(object state)
      {
         ConnectDB.LoadData(users);
         try {
            for (int i = 0; i < users.Count; i++) {
               if (users[i].sub != "none" && users[i].sub != null) {
                  ParseKwork(users[i].user_id);
               }
            }
            string request = string.Empty;
            string[] doc = File.ReadAllText(Path.GetFullPath("soldTemp.txt")).Split('§');
            for (int i = 0; i < doc.Length; i++) {
               try {
                  if (!request.Contains(doc[i].Split('▼')[4].Trim('\r').Trim('\n')) && doc[i].Contains("▼")) request += "insert into `Order` (title, category, price, text, order_id) values ('" + doc[i].Split('▼')[0].Trim('\r').Trim('\n') + "', '" + doc[i].Split('▼')[1].Trim('\r').Trim('\n') + "', '" + doc[i].Split('▼')[2].Trim('\r').Trim('\n') + "', '" + doc[i].Split('▼')[3].Trim('\r').Trim('\n') + "', '" + doc[i].Split('▼')[4].Trim('\r').Trim('\n') + "');\n";
               } catch { }
            }
            File.WriteAllText(Path.GetFullPath("soldTemp.txt"), "");
            ConnectDB.Query(request);
            ConnectDB.LoadOrder(orders);
            if(orders.Count > 2500) {
               int count = orders.Count - 2500;
               string del = string.Empty;
               for (int i = 0; i < count; i++) del += "delete from `Order` where id = " + orders[i].id + ";\n";
               ConnectDB.Query(del);
            }
         } catch { }
      }

      private static async void ParseKwork(string chatId)
      {
         ConnectDB.LoadData(users);
         string checkCategory = users.Find(item => item.user_id == chatId).categories;
         string[] result = new string[0];
         string[] sources = new string[checkCategory.Split('|').Length];
         if (checkCategory.Contains('|')) for (int i = 0; i < checkCategory.Split('|').Length; i++) sources[i] = checkCategory.Split('|')[i].Split('*')[1];
         else if (checkCategory.Contains("kwork")) { Array.Resize(ref sources, 1); sources[0] = checkCategory; }
         else return;
         for (int i = 0; i < sources.Length; i++) {

            string html = string.Empty;
            try {
               WebClient wc = new WebClient();
               html = wc.DownloadString(sources[i].Split('‼')[1]);
               Thread.Sleep(3000);
            } catch { break; }
            for (int j = 0; j < 12; j++) {
               try {
                  string title = string.Empty, description = String.Empty, price = String.Empty, idOrder = String.Empty;
                  int index = -1;
                  index = html.IndexOf("class=\"card want-card js-card-") + "class=\"card want-card js-card-".Length; html = html.Remove(0, index);
                  index = html.IndexOf(" ");
                  idOrder = html.Remove(index, html.Length - index).Replace("ta-id=\"", string.Empty);
                  if (Convert.ToInt32(idOrder) >= 0 && Convert.ToInt32(idOrder) <= 99999999) {
                     ConnectDB.LoadOrder(orders);
                     string find = string.Empty;
                     try {
                        find = orders.Find(item => item.order_id == idOrder).order_id;
                     } catch { }
                     if (find == "" || find == null || find == string.Empty) {
                        try {
                           index = html.IndexOf("class=\"card want-card js-card-");
                           string newHtml = string.Empty;
                           newHtml = html.Remove(index, html.Length - index);
                           index = newHtml.IndexOf("0\"><a href=\"https://kwork.ru/projects/" + idOrder + "\">") + ("0\"><a href=\"https://kwork.ru/projects/" + idOrder + "\">").Length; newHtml = newHtml.Remove(0, index);
                           index = newHtml.IndexOf("</a>");
                           title = newHtml.Remove(index, newHtml.Length - index);
                           if (newHtml.Contains("hidden\"><div class=\"wants-card__space\"></div>")) index = newHtml.IndexOf("hidden\"><div class=\"wants-card__space\"></div>") + "hidden\"><div class=\"wants-card__space\"></div>".Length;
                           else if (newHtml.Contains("js-want-block-toggle-summary\"><div class=\"wants-card__space\"></div>")) index = newHtml.IndexOf("js-want-block-toggle-summary\"><div class=\"wants-card__space\"></div>") + "js-want-block-toggle-summary\"><div class=\"wants-card__space\"></div>".Length;
                           else if (newHtml.Contains("js-want-block-toggle js-want-block-toggle-summary\">")) index = newHtml.IndexOf("js-want-block-toggle js-want-block-toggle-summary\">") + "js-want-block-toggle js-want-block-toggle-summary\">".Length;
                           else if (newHtml.Contains("first-letter\"><div class=\"wants-card__space\"></div>")) index = newHtml.IndexOf("first-letter\"><div class=\"wants-card__space\"></div>") + "first-letter\"><div class=\"wants-card__space\"></div>".Length;
                           else description = "Ошибка в чтении описания";
                           if (description.Length == 0) {
                              newHtml = newHtml.Remove(0, index);
                              if (newHtml.Contains("&nbsp;<a href=\"")) index = newHtml.IndexOf("&nbsp;<a href=\"");
                              else index = newHtml.IndexOf("</div></div></div><div");
                              description = newHtml.Remove(index, newHtml.Length - index).Replace("<br />", string.Empty).Replace("&quot;", "\"").Replace("&middot;", "▫️").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&lt;br />", string.Empty).Replace("&laquo;", "«").Replace("&raquo;", "»").Replace("&mdash;", "—");
                              if (description.Contains("<span class=\"message-emoji-icon message-emoji")) {
                                 int tempIndex = description.IndexOf("<span class=\"message-emoji-icon message-emoji");
                                 string tempDescription = description.Remove(0, tempIndex);
                                 description = tempDescription;
                                 tempIndex = description.IndexOf(";\"></span>") + ";\"></span>".Length;
                                 tempDescription = description.Remove(tempIndex, description.Length);
                                 description += tempDescription;
                              }
                              if (description.Contains("</div> <div id=\"list-files\" class=\"files-list mt10\">")) {
                                 int tempIndex = description.IndexOf("</div> <div id=\"list-files\" class=\"files-list mt10\">");
                                 string tempDescription = description.Remove(0, tempIndex);
                                 description = tempDescription;
                                 tempIndex = description.IndexOf("\"rouble\">₽</span>") + "\"rouble\">₽</span>".Length;
                                 tempDescription = description.Remove(tempIndex, description.Length);
                                 description += tempDescription;
                              }
                           }
                           bool checkSource = false;
                           while (checkSource == false) {
                              if (description.Contains("<a rel=\"nofollow")) {
                                 index = description.IndexOf("<a rel=\"nofollow");
                                 int indexOut = description.IndexOf("\">") + "\">".Length;
                                 description = description.Remove(index, indexOut - index).Replace("</a>", string.Empty);
                              }
                              else checkSource = true;
                           }
                           index = newHtml.IndexOf("wants-card__price mt10\"><span class=\"fs12\">") + "wants-card__price mt10\"><span class=\"fs12\">".Length; newHtml = newHtml.Remove(0, index);
                           index = newHtml.IndexOf("&nbsp;<span");
                           price = newHtml.Remove(index, newHtml.Length - index).Replace("</span>", string.Empty) + " руб.";
                           Array.Resize(ref result, result.Length + 1);
                           result[result.Length - 1] = idOrder + "▼*" + title + "*\n\n*Категория:* " + sources[i].Split('‼')[0] + "\n*" + price.Split(':')[0] + ":* " + price.Split(':')[1] + "\n-----\n" + new string(description.Take(200).ToArray()).Trim('\r').Trim('\n') + "...";
                           StreamWriter sw = new StreamWriter("soldTemp.txt", true, Encoding.UTF8);
                           sw.WriteLine(title + "▼" + sources[i].Split('‼')[0] + "▼" + price + "▼" + description + "▼" + idOrder + "§");
                           sw.Close();
                        } catch { }
                     }
                  }
               } catch { Console.WriteLine("main"); }
            }
         }
         for (int i = 0; i < result.Length; i++) {
            try {
               var keyborad = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithUrl("На kwork.ru", "https://kwork.ru/projects/" + result[i].Split('▼')[0]), InlineKeyboardButton.WithCallbackData("Весь текст", "FullText_" + result[i].Split('▼')[0]) } });
               await client.SendTextMessageAsync(Convert.ToInt32(chatId), result[i].Split('▼')[1], replyMarkup: keyborad, disableWebPagePreview: true, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            } catch { Console.WriteLine("send"); }
         }
      }

      private static async void TimerMessage(Telegram.Bot.Types.Message message)
      {
         try {
            Thread.Sleep(5000);
            await client.DeleteMessageAsync(message.Chat.Id, message.MessageId);
         } catch { }
      }
   }
}
