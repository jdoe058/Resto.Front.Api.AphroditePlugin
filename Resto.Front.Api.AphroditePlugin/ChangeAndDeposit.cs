using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Payments;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


namespace Resto.Front.Api.AphroditePlugin
{
    internal class ChangeAndDeposit
    {
        public decimal Change { get; }      // сдача

        public decimal Payments { get; }    // внесено

        public decimal Deposit { get; }

        public decimal UnusedDeposit { get; }

        public decimal NeedToPay { get; }

        public ChangeAndDeposit(IOrder order)
        {
            Payments    = order.Payments.Sum(x => x.Sum);
            Change      = Payments - order.ResultSum;
            NeedToPay   = order.ResultSum - Payments;

            if (int.TryParse(order.GetOrderTypeNameSafe(), out int result))
            {
                Deposit = result;
                UnusedDeposit = Math.Max(result - order.GetDepositProducts().Sum(x => x.ResultSum), 0);
                Change -= UnusedDeposit;
                NeedToPay += UnusedDeposit;
            }

            Change = Math.Max(Change, 0);
            NeedToPay = Math.Max(NeedToPay, 0);
        }

        public List<XElement> ToCells()
        {
            List<XElement> xelementList = new List<XElement>();
            if (Deposit > 0)
            {
                xelementList.Add(new XElement("c", "Депозит"));
                xelementList.Add(new XElement("c", Deposit));
            }
            if (UnusedDeposit > 0)
            {
                xelementList.Add(new XElement("c", "Неисп. депозит"));
                xelementList.Add(new XElement("c", UnusedDeposit));
            }
            if (Change > 0)
            {
                xelementList.Add(new XElement("c", "Сдача"));
                xelementList.Add(new XElement("c", Change));
            }
            return xelementList;
        }

        public override string ToString() => string.Format("Внесено {0}, Неисп. депозит: {1}, Нужно внести: {2} Сдача: {3}", Payments, UnusedDeposit, NeedToPay, Change);
    }
}
