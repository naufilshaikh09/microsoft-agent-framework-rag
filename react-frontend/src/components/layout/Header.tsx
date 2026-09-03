import { MessageSquarePlus } from 'lucide-react'

interface Props {
  onNewChat?: () => void
}

export function Header({ onNewChat }: Props) {
  return (
    <header className="h-12 flex items-center justify-between px-4 border-b border-[#2e2e2e] bg-[#111111] flex-shrink-0">
      <span className="text-sm text-gray-500">New Conversation</span>
      {onNewChat && (
        <button
          onClick={onNewChat}
          className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1 rounded-md hover:bg-[#2e2e2e]"
          title="New chat"
        >
          <MessageSquarePlus size={14} />
          New chat
        </button>
      )}
    </header>
  )
}
