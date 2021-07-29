using Resto.Front.Api.AphroditePlugin.Properties;

using Resto.Front.Api.Data.Assortment;
using Resto.Front.Api.Data.Cheques;
using Resto.Front.Api.Data.Common;
using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Payments;
using Resto.Front.Api.Data.View;
using Resto.Front.Api.Editors;
using Resto.Front.Api.Editors.Stubs;
using Resto.Front.Api.Extensions;
using Resto.Front.Api.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Xml.Linq;


namespace Resto.Front.Api.AphroditePlugin
{
    internal sealed class Transactions : IDisposable
    {
        private readonly CompositeDisposable subscriptions;

        public void Dispose() => this.subscriptions.Dispose();

        public Transactions()
        {
            subscriptions = new CompositeDisposable()
            {
                PluginIntegrationServiceExtensions.AddButton(PluginContext.Integration, "Караоке: Информация",  (v, p) => {
                    if (v.TryGetOrderByCard(out IOrder result)) {
                        ChangeAndDeposit changeAndDeposit = new ChangeAndDeposit(result);
                        PluginContext.Operations.AddNotificationMessage(changeAndDeposit.ToString(), "Info", TimeSpan.FromSeconds(20.0));
                    }
                }),

                NotificationServiceExtensions.SubscribeOnBillChequePrinting(PluginContext.Notifications,  x => new BillCheque() {
                    AfterHeader = new XElement("left", string.Format("Дата: {0}", DateTime.Now.ToString("g"))),
                    BeforeFooter = PluginContext.Operations.GetOrderById(x).GetPayments()
                }),

                NotificationServiceExtensions.SubscribeOnCashChequePrinting(PluginContext.Notifications,  x => new CashCheque() {
                    AfterCheque = PluginContext.Operations.GetOrderById(x).GetPayments()
                }),

                NotificationServiceExtensions.SubscribeOnBeforeServiceCheque(PluginContext.Notifications,  (order, i, v) => {
                    ChangeAndDeposit changeAndDeposit = new ChangeAndDeposit(order);
                    if (changeAndDeposit.NeedToPay > 0) 
                    {
                        v.ShowErrorPopup(string.Format("Нужно внести: {0}", changeAndDeposit.NeedToPay));
                        throw new OperationCanceledException( );
                    }
                })
            };

            if (!Settings.Default.ShowTransactions) return;

            subscriptions.Add(PluginIntegrationServiceExtensions.AddButton(PluginContext.Integration, "Караоке: Внесено в открытые заказы", 
                (v, p) => v.ShowYesNoPopup("Итого в кассе", PluginContext.Operations
                    .GetOrders().Where(x => x.Status != OrderStatus.Closed && x.Status != OrderStatus.Deleted)
                    .SelectMany(x => x.Payments).Where(x => x.Status != PaymentStatus.Cancelled && x.Status != PaymentStatus.Storned).Sum(x => x.Sum).ToString())));

            subscriptions.Add(PluginIntegrationServiceExtensions.AddButton(PluginContext.Integration, "Караоке: Внести", (v, p) => {
                if (v.TryGetOrderByCard(out IOrder result)) {
                    NumberInputDialogResult inputDialogResult = (NumberInputDialogResult)v.ShowInputDialog("Введите сумму", InputDialogTypes.Number);

                    if (inputDialogResult != null)
                    {

                        decimal number = inputDialogResult.Number;

                        string str = DateTime.Now.ToShortTimeString() + " Внесено " + number.ToString();

                        string externalDataByKey = PluginContext.Operations.TryGetOrderExternalDataByKey(result.Id, nameof(Transactions));

                        if (!string.IsNullOrWhiteSpace(externalDataByKey))
                            str += ", " + externalDataByKey;

                        IEditSession editSession = PluginContext.Operations.CreateEditSession();

                        try
                        {
                            IPaymentItem ipaymentItem = result.Payments.Last(x => x.IsExternal);
                            number += ipaymentItem.Sum;
                            str += ipaymentItem.AdditionalData is ExternalPaymentItemAdditionalData additionalData6 ? additionalData6.CustomData : null;
                            PluginContext.Operations.AddNotificationMessage(str, "", new TimeSpan?(TimeSpan.FromSeconds(5.0)));
                            editSession.DeleteExternalPaymentItem(ipaymentItem, result);
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        editSession.AddOrderExternalData(nameof(Transactions), str, result);
                        IEditSession ieditSession = editSession;
                        Decimal num = number;
                        ExternalPaymentItemAdditionalData itemAdditionalData = new ExternalPaymentItemAdditionalData();
                        itemAdditionalData.CustomData = str;
                        IPaymentType ipaymentType = PluginContext.Operations.GetPaymentTypes().First(x => x.Kind == PaymentTypeKind.Cash);

                        IOrder iorder = result;
                        ieditSession.AddExternalPaymentItem(num, true, itemAdditionalData, ipaymentType, iorder);
                        PluginContext.Operations.SubmitChanges(PluginContext.Operations.GetCredentials(), editSession);

                        IOrder orderById = PluginContext.Operations.GetOrderById(result.Id);

                        p.Print(orderById.Slip());
                    }
                }}));

            subscriptions.Add(PluginIntegrationServiceExtensions.AddButton(PluginContext.Integration, "Караоке: Оплата", (v, p) => {
                if (v.TryGetOrderByCard(out IOrder result)) {
                    bool flag = false;
                    ChangeAndDeposit changeAndDeposit = new ChangeAndDeposit(result);
                    IEditSession editSession = PluginContext.Operations.CreateEditSession();
                        
                    if (changeAndDeposit.UnusedDeposit > 0)
                    {
                        if (!v.ShowYesNoPopup("Неисп. депозит", string.Format("В заказе неиспользовано {0}. Продолжить оплату?", changeAndDeposit.UnusedDeposit)))
                            return;

                        if (result.Status == OrderStatus.Bill)
                        {
                            PluginContext.Operations.CancelBill(PluginContext.Operations.GetCredentials(), result);
                            result = PluginContext.Operations.GetOrderById(result.Id);
                        }

                        editSession.AddOrderProductItem(changeAndDeposit.UnusedDeposit / 1000, 
                            PluginContext.Operations.GetActiveProducts().Last(x => x.Number == Settings.Default.UnusedDepositProductNumber), 
                            result, result.Guests.Last(), null);
                        flag = true;
                    }

                    if (changeAndDeposit.Change > 0)
                    {
                        string str = nameof(Transactions);
                        //editSession.AddOrderExternalData(str, DateTime.Now.ToShortTimeString() + " Сдача " + changeAndDeposit.Change.ToString() + ", " + PluginContext.Operations.TryGetOrderExternalDataByKey(result.Id, str), result);
                        editSession.AddOrderExternalData(str, string.Format("{0} Сдача {1}, {2}", 
                            DateTime.Now.ToShortTimeString(), changeAndDeposit.Change.ToString(), PluginContext.Operations.TryGetOrderExternalDataByKey(result.Id, str)), result);
                        try
                        {
                            editSession.DeleteExternalPaymentItem(result.Payments.Last(x => x.IsExternal), result);
                        }
                        catch (InvalidOperationException )
                        {
                        }
                        editSession.AddExternalPaymentItem(result.ResultSum + changeAndDeposit.UnusedDeposit, true, null, PluginContext.Operations.GetPaymentTypes().First(x => x.Kind == PaymentTypeKind.Cash), result);
                        flag = true;
                    }

                    if (flag)
                        PluginContext.Operations.SubmitChanges(PluginContext.Operations.GetCredentials(), editSession);

                    IOrder orderById = PluginContext.Operations.GetOrderById(result.Id);
                    ReceiptSlip receiptSlip = orderById.Slip(true);
                    p.Print(receiptSlip);
                    p.Print(receiptSlip);
                    PluginContext.Operations.PayOrder(PluginContext.Operations.GetCredentials(), orderById);
                    if (changeAndDeposit.Change > 0)
                        v.ShowYesNoPopup("Сдача", changeAndDeposit.Change.ToString());
                }}));
        }
    }
}
