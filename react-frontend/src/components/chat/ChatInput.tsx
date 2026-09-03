import { useState, type KeyboardEvent } from 'react'
import { Send, Square } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Props {
  onSend: (message: string) => void
  onStop?: () => void
  disabled?: boolean
  isStreaming?: boolean
}

export function ChatInput({ onSend, onStop, disabled, isStreaming }: Props) {
  const [value, setValue] = useState('')

  const handleSend = () => {
    const trimmed = value.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setValue('')
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="flex items-end gap-2 p-4 border-t border-[#2e2e2e] bg-[#0f0f0f]">
      <textarea
        value={value}
        onChange={e => setValue(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={isStreaming ? 'Waiting for response...' : 'Ask anything about your documents... (Enter to send, Shift+Enter for newline)'}
        disabled={disabled && !isStreaming}
        rows={1}
        className={cn(
          'flex-1 resize-none rounded-xl bg-[#1a1a1a] border border-[#2e2e2e]',
          'px-4 py-3 text-sm text-gray-100 placeholder-gray-600',
          'focus:outline-none focus:border-violet-500 transition-colors',
          'disabled:opacity-40 disabled:cursor-not-allowed',
          'max-h-40 overflow-y-auto'
        )}
        style={{ minHeight: '48px' }}
      />
      {isStreaming ? (
        <button
          onClick={onStop}
          className="flex-shrink-0 rounded-xl p-3 bg-[#2e2e2e] text-gray-300 hover:bg-[#3e3e3e] transition-colors"
          aria-label="Stop streaming"
          title="Stop"
        >
          <Square size={18} />
        </button>
      ) : (
        <button
          onClick={handleSend}
          disabled={disabled || !value.trim()}
          className={cn(
            'flex-shrink-0 rounded-xl p-3 bg-violet-600 text-white',
            'hover:bg-violet-500 disabled:opacity-40 disabled:cursor-not-allowed',
            'transition-colors'
          )}
          aria-label="Send message"
          title="Send (Enter)"
        >
          <Send size={18} />
        </button>
      )}
    </div>
  )
}
