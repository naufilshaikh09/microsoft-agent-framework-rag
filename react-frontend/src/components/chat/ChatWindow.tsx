import { useEffect, useRef } from 'react'
import { MessageSquare } from 'lucide-react'
import { MessageBubble } from './MessageBubble'
import { ChatInput } from './ChatInput'
import { useChat } from '@/hooks/useChat'

export function ChatWindow() {
  const { messages, isStreaming, sendMessage, clearMessages, stopStreaming } = useChat()
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  return (
    <div className="flex flex-col flex-1 min-h-0">
      {messages.length > 0 && (
        <div className="flex items-center justify-end px-4 py-2 border-b border-[#2e2e2e]">
          <button
            onClick={clearMessages}
            className="text-xs text-gray-500 hover:text-gray-300 transition-colors"
          >
            Clear chat
          </button>
        </div>
      )}

      <div className="flex-1 overflow-y-auto px-4 py-6">
        {messages.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-center">
            <div className="w-12 h-12 rounded-2xl bg-violet-600/20 border border-violet-600/30 flex items-center justify-center">
              <MessageSquare size={24} className="text-violet-400" />
            </div>
            <div>
              <p className="text-gray-300 font-medium">Start a conversation</p>
              <p className="text-gray-600 text-sm mt-1">
                Ask questions about your documents. Upload files in the sidebar to enable RAG.
              </p>
            </div>
          </div>
        ) : (
          messages.map(msg => <MessageBubble key={msg.id} message={msg} />)
        )}
        <div ref={bottomRef} />
      </div>

      <ChatInput
        onSend={sendMessage}
        onStop={stopStreaming}
        disabled={isStreaming}
        isStreaming={isStreaming}
      />
    </div>
  )
}
