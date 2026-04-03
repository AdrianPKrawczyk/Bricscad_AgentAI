import json
import re
import uuid
import sys

# Słownik mapowania nazw (z V1 na V2)
# Jeśli zmienisz nazwę w C#, zmień ją tutaj.
TOOL_MAPPING = {
    "CREATE_OBJECT": "CreateObject",
    "SELECT": "SelectEntities",
    "SET_PROPERTIES": "SetProperties",
    "MANAGE_LAYERS": "ManageLayers"
}

def validate_logic(raw_tag):
    """
    Sprawdza tag V1 i zwraca odpowiadającą mu nazwę narzędzia w V2.
    """
    clean_tag = raw_tag.replace('ACTION:', '').strip().upper()
    
    if clean_tag in TOOL_MAPPING:
        return TOOL_MAPPING[clean_tag]
    
    # Jeśli nie ma w słowniku, próbujemy auto-konwersji na PascalCase
    # np. CREATE_OBJECT -> CreateObject
    pascal_case = "".join([x.capitalize() for x in clean_tag.split('_')])
    print(f"OSTRZEŻENIE: Brak {clean_tag} w TOOL_MAPPING. Używam auto-konwersji: {pascal_case}")
    return pascal_case

def convert_v1_to_v2(input_file, output_file):
    # Regex obsługujący [ACTION:NAZWA {json}] oraz [NAZWA {json}]
    # Przechwytuje wszystko między '[' a pierwszym '{' jako grupę 1
    pattern = r'\[([A-Z_:]+)\s*(\{.*\})\]'
    
    with open(input_file, 'r', encoding='utf-8') as f_in, \
         open(output_file, 'w', encoding='utf-8') as f_out:
        
        count = 0
        for line in f_in:
            if not line.strip(): continue
            data = json.loads(line)
            new_messages = []
            
            for msg in data['messages']:
                if msg['role'] == 'assistant' and '[' in (msg.get('content') or ''):
                    match = re.search(pattern, msg['content'])
                    if match:
                        raw_tag = match.group(1)
                        args_json = match.group(2)
                        
                        # Pobieramy poprawną nazwę z Twojej logiki mapowania
                        tool_name = validate_logic(raw_tag)
                        
                        tool_call = {
                            "id": f"call_{uuid.uuid4().hex[:12]}",
                            "type": "function",
                            "function": {
                                "name": tool_name,
                                "arguments": args_json
                            }
                        }
                        new_messages.append({
                            "role": "assistant",
                            "content": None,
                            "tool_calls": [tool_call]
                        })
                    else:
                        new_messages.append(msg)
                else:
                    new_messages.append(msg)
            
            json.dump({"messages": new_messages}, f_out, ensure_ascii=False)
            f_out.write('\n')
            count += 1
            
    print(f"Sukces! Skonwertowano {count} przykładów do pliku: {output_file}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Użycie: python migrate_v1_to_v2.py <input.jsonl> <output.jsonl>")
    else:
        convert_v1_to_v2(sys.argv[1], sys.argv[2])