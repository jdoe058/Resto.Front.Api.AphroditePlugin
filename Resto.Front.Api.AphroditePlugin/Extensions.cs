
using Resto.Front.Api.AphroditePlugin.Properties;
using Resto.Front.Api;
using Resto.Front.Api.Data.Assortment;
using Resto.Front.Api.Data.Brd;
using Resto.Front.Api.Data.Cheques;
using Resto.Front.Api.Data.Common;
using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Payments;
using Resto.Front.Api.Data.Search;
using Resto.Front.Api.Data.View;
using Resto.Front.Api.Editors;
using Resto.Front.Api.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


namespace Resto.Front.Api.AphroditePlugin
{
    public static class Extensions
    {
        public static bool TryGetOrderByCard(this IViewManager view,  out IOrder result,  string cardNumber = null)
        {
            result = null;
            if (cardNumber == null)
            {
                switch (view.ShowExtendedInputDialog("Поиск заказа", "Прокатайте карту или введите ее номер", new ExtendedInputDialogSettings()
                {
                    EnableCardSlider = true,
                    EnableNumericString = true,
                    TabTitleNumericString = "Номер карты"
                }))
                {
                    case null:
                        return false;
                    case NumericStringInputDialogResult inputDialogResult3:
                        cardNumber = inputDialogResult3.NumericString;
                        break;
                    case CardInputDialogResult inputDialogResult4:
                        cardNumber = inputDialogResult4.Track2;
                        break;
                    default:
                        throw new NotSupportedException("GetType");
                }
            }

            IReadOnlyList<IClient> iclientList = PluginContext.Operations.SearchClients(cardNumber, (SearchType)1, (ClientFields)2);
            if (iclientList.Count == 0)
            {
                if (!view.ShowYesNoPopup("Карта не найдена", string.Format("Зарегистрировать анонимного гостя с картой № {0}?", cardNumber)))
                    return false;
                IEditSession editSession = PluginContext.Operations.CreateEditSession();
                editSession.CreateClient(Guid.NewGuid(), cardNumber, null, cardNumber, new DateTime?(DateTime.Now));
                PluginContext.Operations.SubmitChanges(PluginContext.Operations.GetCredentials(), editSession);
                iclientList = PluginContext.Operations.SearchClients(cardNumber, SearchType.Equals, ClientFields.CardNumber);
            }
            try
            {
                Guid client = iclientList.Last().Id;
                IOrder iorder = PluginContext.Operations.GetOrders().Where(x => x.Status == null || x.Status == OrderStatus.Bill || x.Status == OrderStatus.New).Last(x => x.CustomerIds.Contains(client));
                result = iorder;
                return true;
            }
            catch (InvalidOperationException)
            {
                PluginContext.Operations.AddWarningMessage(string.Format("Карта № {0} не привязана к заказу", cardNumber), "Заказ не найден", new TimeSpan?(TimeSpan.FromSeconds(20.0)));
            }
            return false;
        }

        private static string GetProductCategoryNameSafe(this IProduct i) => i.Category?.Name ?? string.Empty;

        public static string GetOrderTypeNameSafe(this IOrder order) => order.OrderType?.Name ?? string.Empty;

        public static IEnumerable<IOrderProductItem> GetDepositProducts(this IOrder order)
        {
            return order.Items.OfType<IOrderProductItem>().Where(x => !x.Deleted).Where(x => !Settings.Default.DepositExtensions.Contains(x.Product.GetProductCategoryNameSafe()));
        }

        public static XElement GetPayments(this IOrder order)
        {
            ChangeAndDeposit changeAndDeposit = new ChangeAndDeposit(order);
            return new XElement("table", 
                new XElement( "columns", 
                    new XElement("column", new XAttribute("formatter", "split")),
                    new XElement("column", new XAttribute("align", "right"), new XAttribute("width", "10"))),

                new XElement("cells", 
                    order.Payments.Where( x => x.IsExternal).SelectMany( x =>  (new XElement[]
                    {
                        new XElement("ct", x.Type.Name), 
                        new XElement("c", x.Sum.ToString("C")),
                        new XElement("c", PluginContext.Operations.TryGetOrderExternalDataByKey(order.Id, "Transactions"), new XAttribute("colspan", "2"))
                    })),
                changeAndDeposit.ToCells()));
        }

        private static XElement Sales(this IOrder order, bool isBay = false) => new XElement("table", 
            new XElement("columns", 
                new XElement("column", new XAttribute("formatter", "split")),
                new XElement("column", new XAttribute("align", "right"), new XAttribute("autowidth", "")),
                new XElement("column", new XAttribute("align", "right"), new XAttribute("width", "10"))),

            new XElement( "cells", 
                new XElement("ct", "Наименование"),
                new XElement("ct", "Кол-во"),
                new XElement("ct", "Сумма"),
                new XElement( "linecell"),
                order.Items.OfType<IOrderProductItem>().Where( x => ! x.Deleted && !string.IsNullOrWhiteSpace(x.Product?.Name)).SelectMany( x =>  (new XElement[]
                {
                    new XElement("c", x.Product.Name),
                    new XElement("c", x.Amount),
                    new XElement("c", x.ResultSum.ToString("C"))
                }))));

        public static ReceiptSlip Slip(this IOrder order, bool IsBuy = false, string CashierName = "", int CashRegisterNumber = 1, int SessionNumber = 2, bool IsStorno = false)
        {
            return new ReceiptSlip() { Doc = new XElement("doc", 
                new XElement("line", new XAttribute( "symbols",  "*")),
                new XElement("line", new XAttribute( "symbols",  "*")),
                new XElement("pair", new XAttribute( "left",  string.Format("Касса: {0}",  CashRegisterNumber)), new XAttribute( "right", "Караоке") ),
                new XElement("pair", new XAttribute( "left",  "Кассовая смена:"), new XAttribute( "right",  SessionNumber) ),
                new XElement("center", IsStorno ?  "Квитанция о возврате заказа" : (IsBuy ?  "Квитанция об оплате заказа" :  "Квитанция об оплате")),
                new XElement("pair", new XAttribute( "left",  "Дата:"), new XAttribute( "right",  DateTime.Now.ToString("g")) ),
                new XElement("pair", new XAttribute( "left",  string.Format("Кассир: {0}",  CashierName)), new XAttribute( "right",  string.Format("Касса № {0}",  order.Number))),
                new XElement("left", string.Format("Официант: {0}", order.Waiter.Name)),
                new XElement("line"),
                IsBuy ? order.Sales() : new XElement("left", string.Format("Внесение за заказ {0}", order.Number)),
                new XElement("line"), 
                new XElement("pair", new XAttribute("left", "Итого к оплате:"), new XAttribute("right", order.ResultSum)), 
                order.GetPayments(),
                new XElement("center", string.Format("ВСЕ СУММЫ В {0}", "РУБЛЯХ")))}; 
        }
    }
}
