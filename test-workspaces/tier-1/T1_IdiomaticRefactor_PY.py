import math

def calculate_stats(numbers):
    total = 0
    for n in numbers:
        total = total + n
    average = total / len(numbers)
    
    variance = 0
    for n in numbers:
        variance = variance + (n - average) ** 2
    variance = variance / len(numbers)
    
    return total, average, math.sqrt(variance)

def find_duplicates(items):
    duplicates = []
    for i in range(len(items)):
        for j in range(len(items)):
            if i != j and items[i] == items[j]:
                if items[i] not in duplicates:
                    duplicates.append(items[i])
    return duplicates

def parse_csv_line(line):
    result = []
    current = ""
    for char in line:
        if char == ",":
            result.append(current)
            current = ""
        else:
            current = current + char
    result.append(current)
    return result

data = [4, 8, 15, 16, 23, 42]
total, avg, std_dev = calculate_stats(data)
print("Total: " + str(total))
print("Average: " + str(avg))
print("Std Dev: " + str(std_dev))

items = [1, 2, 3, 2, 4, 3, 5]
print("Duplicates: " + str(find_duplicates(items)))

line = "hello,world,foo,bar"
print("Parsed: " + str(parse_csv_line(line)))