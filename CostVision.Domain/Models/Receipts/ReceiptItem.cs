using CostVision.Domain.Models.Enums.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Позиция чека.
    /// </summary>
    public class ReceiptItem : IEntity<Guid>
    {
        private ReceiptItem()
        {
        }

        public Guid Id { get; set; }

        public decimal Price { get; private set; }

        public decimal Quantity { get; private set; }

        public decimal Sum { get; private set; }

        public int Nds { get; private set; }

        public PaymentType PaymentType { get; private set; }

        public ProductType ProductType { get; private set; }

        public QuantityMeasureType ItemsQuantityMeasure { get; private set; }

        public Guid ReceiptId { get; private set; }

        public Guid? ProductId { get; private set; }

        public Guid? CategoryId { get; private set; }

        public Receipt? Receipt { get; private set; }

        public ExpenseCategory? Category { get; private set; }

        public Product? Product { get; private set; }

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

        internal bool TryAttachTo(Receipt receipt, out string? error)
        {
            if (!CanAttachTo(receipt, out error))
                return false;

            Receipt = receipt;
            ReceiptId = receipt.Id;
            return true;
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
