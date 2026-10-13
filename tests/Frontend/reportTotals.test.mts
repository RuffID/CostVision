import { test } from "node:test";
import assert from "node:assert/strict";
import { updateReceiptsSummary } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/render.js";
import { createStoreRow } from "../../CostVision.Web/wwwroot/dist/pages/stores/render.js";

class ElementStub {
    children: ElementStub[] = [];
    textContent = "";
    className = "";
    type = "";
    dataset: Record<string, string> = {};
    style = {};
    classList = { add() {}, toggle() {} };
    append(...children: ElementStub[]): void {
        this.children.push(...children);
    }
    addEventListener(): void {}
}

class ButtonStub extends ElementStub {}

test("receipt summary uses full server totals and separates income from expense", () => {
    // Arrange
    const count = new ElementStub();
    const sums = new ElementStub();
    const response = { totalCount: 4, totalSum: 1070.45, incomeSum: 350.25,
        items: [{ totalSum: 1000 }, { totalSum: 300 }] };

    // Act
    updateReceiptsSummary(count, sums, response, value => value.toFixed(2) + " ₽");

    // Assert
    assert.equal(count.textContent, "Чеков: 4");
    assert.equal(sums.textContent, "Расход: 1070.45 ₽ · Доход: 350.25 ₽");
});

test("unknown receipt totals are displayed as unknown without invented zero", () => {
    // Arrange
    const count = new ElementStub();
    const sums = new ElementStub();

    // Act
    updateReceiptsSummary(count, sums, null, value => value.toFixed(2));

    // Assert
    assert.equal(count.textContent, "Чеков: —");
    assert.equal(sums.textContent, "Итоги ещё не загружены");
    assert.doesNotMatch(sums.textContent, /0/);
});

test("store row displays separate monetary columns with kopecks", () => {
    // Arrange
    const previousDocument = globalThis.document;
    const previousButton = globalThis.HTMLButtonElement;
    Object.assign(globalThis, {
        document: { createElement: tag => tag === "button" ? new ButtonStub() : new ElementStub() },
        HTMLButtonElement: ButtonStub
    });
    try {
        const store = { id: "store", name: "Store", address: "Address", adaptiveName: null,
            displayName: "Store", receiptCount: 2, totalSpent: 1000.45, incomeSum: 300.25,
            groupKey: "STORE", children: [] };

        // Act
        const row = createStoreRow(store, { showOriginalNames: true });

        // Assert
        assert.equal(row.children.length, 5);
        assert.match(row.children[3].textContent, /1\s000,45\s₽/);
        assert.match(row.children[4].textContent, /300,25\s₽/);
    }
    finally {
        Object.assign(globalThis, { document: previousDocument, HTMLButtonElement: previousButton });
    }
});
