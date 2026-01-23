import json
import sys

def minimize_json(input_file, output_file):
    try:
        with open(input_file, 'r') as file:
            data = json.load(file)
        with open(output_file, 'w') as file:
            json.dump(data, file, separators=(',', ':'))  
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = "output.json"

    minimize_json(input_file, output_file)