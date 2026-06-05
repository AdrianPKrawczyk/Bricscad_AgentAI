import codecs
import re

with codecs.open('src/UI/AgentControl.cs', 'r', 'utf-8-sig') as f:
    text = f.read()

# Fix Label Width
text = text.replace('Width = 150', 'AutoSize = true, Padding = new Padding(0, 0, 10, 0)')

# Fix hardcoded tokens
text = text.replace('int maxTokens = 8192; // Docelowo pobierane z ustawień',
                    'int maxTokens = LLMConfigManager.Current?.MaxContextTokens > 0 ? LLMConfigManager.Current.MaxContextTokens : (LLMConfigManager.GetActiveProvider()?.MaxTokens ?? 8192);')

with codecs.open('src/UI/AgentControl.cs', 'w', 'utf-8-sig') as f:
    f.write(text)
