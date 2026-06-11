interface PhysicalItem {
    id: string;
    title: string;
    price: number;
    weight: number;
    shippingAddress: string;
}

interface DigitalItem {
    id: string;
    name: string;
    cost: number;
    downloadUrl: string;
    fileSizeMb: number;
}

class ShoppingBasket {
    private physicalItems: PhysicalItem[] = [];
    private digitalItems: DigitalItem[] = [];

    // Redundant methods that do almost the same thing
    public addPhysical(item: PhysicalItem): void {
        console.log(`Adding physical item: ${item.title}`);
        this.physicalItems.push(item);
    }

    public addDigital(item: DigitalItem): void {
        console.log(`Adding digital item: ${item.name}`);
        this.digitalItems.push(item);
    }

    public calculateTotal(): number {
        let total = 0;
        this.physicalItems.forEach(i => total += i.price);
        this.digitalItems.forEach(i => total += i.cost);
        return total;
    }

    public printReceipt(): void {
        console.log("--- YOUR RECEIPT ---");
        this.physicalItems.forEach(i => {
            console.log(`[Physical] ${i.title} - $${i.price}`);
        });
        this.digitalItems.forEach(i => {
            console.log(`[Digital] ${i.name} - $${i.cost}`);
        });
        console.log(`TOTAL: $${this.calculateTotal()}`);
    }
}

// Helper functions that currently require separate overloads or complex logic
function validatePhysical(item: PhysicalItem): boolean {
    return item.id.length > 0 && item.price >= 0 && item.weight > 0;
}

function validateDigital(item: DigitalItem): boolean {
    return item.id.length > 0 && item.cost >= 0 && item.fileSizeMb > 0;
}

// --- Execution Simulation ---

const myBasket = new ShoppingBasket();

myBasket.addPhysical({
    id: "p-001",
    title: "Mechanical Keyboard",
    price: 120,
    weight: 1.5,
    shippingAddress: "123 Main St"
});

myBasket.addDigital({
    id: "d-999",
    name: "TypeScript Mastery E-Book",
    cost: 29.99,
    downloadUrl: "https://example.com/dl/ts-mastery",
    fileSizeMb: 15
});

myBasket.printReceipt();
