using CostVision.Domain.Models.Enums.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Позиция чека.
    /// </summary>
    public class ReceiptItem : IEntity<Guid>
    {
        internal ReceiptItem()
        {
        }

        public Guid Id { get; set; }

        public decimal Price { get; internal set; }

        public decimal Quantity { get; internal set; }

        public decimal Sum { get; internal set; }

        public int Nds { get; internal set; }

        public PaymentType PaymentType { get; internal set; }

        public ProductType ProductType { get; internal set; }

        public QuantityMeasureType ItemsQuantityMeasure { get; internal set; }

        public Guid ReceiptId { get; internal set; }

        public Guid? ProductId { get; internal set; }

        public Guid? CategoryId { get; internal set; }

        public Receipt? Receipt { get; internal set; }

        public ExpenseCategory? Category { get; set; }

        public Product? Product { get; internal set; }

        /// <summary>
        /// Создаёт допустимую позицию чека.
        /// </summary>
        public static bool TryCreate(
            decimal price,
            decimal quantity,
            decimal sum,
            int nds,
            PaymentType paymentType,
            ProductType productType,
            QuantityMeasureType itemsQuantityMeasure,
            Product product,
            Guid? categoryId,
            out ReceiptItem? item,
            out string? error)
        {
            item = null;

            if (product == null)
            {
                error = "Для позиции чека не указан товар.";
                return false;
            }

            if (!TryValidateValues(price, quantity, sum, paymentType, productType, itemsQuantityMeasure, out error))
                return false;

            item = new ReceiptItem
            {
                Price = price,
                Quantity = quantity,
                Sum = sum,
                Nds = nds,
                PaymentType = paymentType,
                ProductType = productType,
                ItemsQuantityMeasure = itemsQuantityMeasure,
                ProductId = product.Id == Guid.Empty ? null : product.Id,
                Product = product,
                CategoryId = categoryId == Guid.Empty ? null : categoryId
            };
            return true;
        }

        internal bool CanAttachTo(Receipt receipt, out string? error)
        {
            if (!TryValidateValues(Price, Quantity, Sum, PaymentType, ProductType, ItemsQuantityMeasure, out error))
                return false;

            if (Product == null)
            {
                error = "Для позиции чека не указан товар.";
                return false;
            }

            if (Receipt != null && !ReferenceEquals(Receipt, receipt))
            {
                error = "Позиция уже принадлежит другому чеку.";
                return false;
            }

            if (ReceiptId != Guid.Empty && ReceiptId != receipt.Id)
            {
                error = "Позиция уже принадлежит другому чеку.";
                return false;
            }

            error = null;
            return true;
        }

        internal void AttachTo(Receipt receipt)
        {
            Receipt = receipt;
            ReceiptId = receipt.Id;
        }

        private static bool TryValidateValues(
            decimal price,
            decimal quantity,
            decimal sum,
            PaymentType paymentType,
            ProductType productType,
            QuantityMeasureType itemsQuantityMeasure,
            out string? error)
        {
            if (price < 0)
            {
                error = "Цена позиции чека не может быть отрицательной.";
                return false;
            }

            if (quantity <= 0)
            {
                error = "Количество позиции чека должно быть больше нуля.";
                return false;
            }

            if (sum < 0)
            {
                error = "Сумма позиции чека не может быть отрицательной.";
                return false;
            }

            if (!Enum.IsDefined(paymentType))
            {
                error = "Некорректный тип оплаты позиции чека.";
                return false;
            }

            if (!Enum.IsDefined(productType))
            {
                error = "Некорректный тип предмета расчёта.";
                return false;
            }

            if (!Enum.IsDefined(itemsQuantityMeasure))
            {
                error = "Некорректная единица измерения позиции чека.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
