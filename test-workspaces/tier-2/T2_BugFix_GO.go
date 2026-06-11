package main

import (
	"fmt"
)

// Order represents a customer purchase in the system.
type Order struct {
	ID          string
	ItemCount   int
	UnitPrice   float64
	IsPremium   bool
	SourceState string
}

// TaxCalculator handles the business logic for taxes and loyalty discounts.
type TaxCalculator struct {
	DefaultTaxRate float64
}

// CalculateTotal computes the final price for an order, applying discounts and state-specific taxes.
func (tc *TaxCalculator) CalculateTotal(order Order) float64 {
	subtotal := float64(order.ItemCount) * order.UnitPrice
	
	discount := 0.0
	// Loyalty Program: Premium customers get a 10% discount if they buy 10 or more items.
	// BUG 1: The requirement is "10 or more" (>=), but the code uses >.
	// This causes customers with exactly 10 items to miss their discount.
	if order.IsPremium && order.ItemCount > 10 {
		discount = subtotal * 0.10
	}
	
	taxableAmount := subtotal - discount
	
	// Tax Application Logic
	var finalTax float64
	if order.SourceState == "NY" {
		// New York has a fixed luxury tax of 8.5%
		finalTax = taxableAmount * 0.085
	} else {
		// Other states use the default rate.
		// BUG 2: Tax should be calculated on the 'taxableAmount' (after discount),
		// but the code incorrectly uses 'subtotal' (before discount).
		finalTax = subtotal * tc.DefaultTaxRate
	}
	
	return taxableAmount + finalTax
}

func main() {
	// Initialize calculator with a 5% default tax rate
	tc := TaxCalculator{DefaultTaxRate: 0.05}
	
	// SCENARIO 1: Premium user with exactly 10 items.
	// Expected: 10 * 100 = 1000. 10% discount = 100. Taxable = 900. 5% Tax = 45. Total = 945.
	// Actual (due to Bug 1): No discount. Taxable = 1000. 5% Tax = 50. Total = 1050.
	order1 := Order{
		ID:          "ORD-1001",
		ItemCount:   10,
		UnitPrice:   100.0,
		IsPremium:   true,
		SourceState: "CA",
	}
	
	// SCENARIO 2: Premium user with 20 items in CA.
	// Expected: 20 * 50 = 1000. 10% discount = 100. Taxable = 900. 5% Tax on 900 = 45. Total = 945.
	// Actual (due to Bug 2): Tax is 5% of 1000 = 50. Total = 950.
	order2 := Order{
		ID:          "ORD-1002",
		ItemCount:   20,
		UnitPrice:   50.0,
		IsPremium:   true,
		SourceState: "CA",
	}

	fmt.Printf("Result for Order %s: $%.2f\n", order1.ID, tc.CalculateTotal(order1))
	fmt.Printf("Result for Order %s: $%.2f\n", order2.ID, tc.CalculateTotal(order2))
}
