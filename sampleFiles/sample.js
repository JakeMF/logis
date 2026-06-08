function calculateStats(numbers) {
    var total = 0;
    for (var i = 0; i < numbers.length; i++) {
        total = total + numbers[i];
    }
    var average = total / numbers.length;

    var variance = 0;
    for (var i = 0; i < numbers.length; i++) {
        variance = variance + Math.pow(numbers[i] - average, 2);
    }
    variance = variance / numbers.length;

    return {
        total: total,
        average: average,
        stdDev: Math.sqrt(variance)
    };
}

function findDuplicates(items) {
    var duplicates = [];
    for (var i = 0; i < items.length; i++) {
        for (var j = 0; j < items.length; j++) {
            if (i != j && items[i] == items[j]) {
                if (duplicates.indexOf(items[i]) == -1) {
                    duplicates.push(items[i]);
                }
            }
        }
    }
    return duplicates;
}

function parseCsvLine(line) {
    var result = [];
    var current = "";
    for (var i = 0; i < line.length; i++) {
        if (line[i] == ",") {
            result.push(current);
            current = "";
        } else {
            current = current + line[i];
        }
    }
    result.push(current);
    return result;
}

function getUserLabel(user) {
    if (user.role == "admin") {
        return "Admin: " + user.name;
    } else if (user.role == "moderator") {
        return "Mod: " + user.name;
    } else {
        return "User: " + user.name;
    }
}

var data = [4, 8, 15, 16, 23, 42];
var stats = calculateStats(data);
console.log("Total: " + stats.total);
console.log("Average: " + stats.average);
console.log("Std Dev: " + stats.stdDev);

var items = [1, 2, 3, 2, 4, 3, 5];
console.log("Duplicates: " + findDuplicates(items));

var line = "hello,world,foo,bar";
console.log("Parsed: " + parseCsvLine(line));

var users = [
    { name: "Alice", role: "admin" },
    { name: "Bob", role: "moderator" },
    { name: "Charlie", role: "user" }
];
for (var i = 0; i < users.length; i++) {
    console.log(getUserLabel(users[i]));
}