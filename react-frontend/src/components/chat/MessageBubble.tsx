import { useState, useRef, useEffect } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import rehypeHighlight from 'rehype-highlight'
import { Copy, Check, AlertCircle, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Message, SourceChunk } from '@/types'

interface Props {
  message: Message
}

function SourcePill({ src }: { src: SourceChunk }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  const scorePercent = Math.round(src.score * 100)

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen(o => !o)}
        className={cn(
          'text-xs px-2 py-0.5 rounded-full border transition-colors',
          'bg-violet-900/40 text-violet-300 border-violet-700/30',
          'hover:bg-violet-800/50 hover:text-violet-200 hover:border-violet-600/50'
        )}
        style={{ opacity: 0.4 + src.score * 0.6 }}
      >
        {src.documentName}{src.pageNumber > 1 ? ` p.${src.pageNumber}` : ''}
        <span className="ml-1 text-violet-400/60">{scorePercent}%</span>
      </button>

      {open && (
        <div className="absolute bottom-full left-0 mb-1 z-50 w-72 rounded-lg bg-[#1a1a2e] border border-violet-800/40 shadow-xl p-3">
          <div className="flex items-start justify-between gap-2 mb-2">
            <div>
              <p className="text-xs font-medium text-violet-300">{src.documentName}</p>
              <p className="text-xs text-gray-500 mt-0.5">
                {src.pageNumber > 1 ? `Page ${src.pageNumber} · ` : ''}Relevance: {scorePercent}%
              </p>
            </div>
            <button onClick={() => setOpen(false)} className="text-gray-600 hover:text-gray-400 flex-shrink-0">
              <X size={13} />
            </button>
          </div>
          <p className="text-xs text-gray-400 leading-relaxed border-t border-[#2e2e2e] pt-2">
            {src.excerpt}
          </p>
        </div>
      )}
    </div>
  )
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  return (
    <button
      onClick={copy}
      className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-600 hover:text-gray-400 absolute top-2 right-2"
      aria-label="Copy response"
    >
      {copied ? <Check size={13} className="text-green-400" /> : <Copy size={13} />}
    </button>
  )
}

export function MessageBubble({ message }: Props) {
  const isUser = message.role === 'user'
  const isThinking = !isUser && message.isStreaming && !message.content

  return (
    <div className={cn('flex w-full mb-4', isUser ? 'justify-end' : 'justify-start')}>
      {!isUser && (
        <div className="flex-shrink-0 w-7 h-7 rounded-full bg-violet-600 flex items-center justify-center text-xs font-bold text-white mr-2 mt-1">
          AI
        </div>
      )}
      <div
        className={cn(
          'relative group max-w-[78%] rounded-2xl px-4 py-3 text-sm leading-relaxed break-words',
          isUser
            ? 'bg-violet-600 text-white rounded-br-sm'
            : 'bg-[#1e1e1e] text-gray-100 rounded-bl-sm border border-[#2e2e2e]'
        )}
      >
        {isThinking && (
          <span className="text-gray-500 italic animate-pulse">Thinking…</span>
        )}

        {message.isError && (
          <div className="flex items-start gap-2 text-red-400">
            <AlertCircle size={14} className="flex-shrink-0 mt-0.5" />
            <span>{message.errorMessage ?? 'An error occurred.'}</span>
          </div>
        )}

        {!message.isError && !isUser && message.content && (
          <>
            <div className="prose prose-invert prose-sm max-w-none
              prose-p:my-1 prose-pre:my-2 prose-ul:my-1 prose-ol:my-1
              prose-pre:bg-[#0d0d1a] prose-pre:border prose-pre:border-[#2e2e2e] prose-pre:rounded-lg
              prose-code:text-violet-300 prose-code:bg-[#1a1a2e] prose-code:px-1 prose-code:rounded
              prose-a:text-violet-400 prose-a:no-underline hover:prose-a:underline">
              <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeHighlight]}>
                {message.content}
              </ReactMarkdown>
            </div>
            {!isUser && !message.isError && <CopyButton text={message.content} />}
          </>
        )}

        {isUser && (
          <span className="whitespace-pre-wrap">{message.content}</span>
        )}

        {message.isStreaming && message.content && (
          <span className="inline-block w-1.5 h-4 ml-0.5 bg-current opacity-75 animate-pulse align-middle" />
        )}

        {!isUser && message.sources && message.sources.length > 0 && (
          <div className="mt-2 pt-2 border-t border-[#2e2e2e] flex flex-wrap gap-1">
            {message.sources.map((src, i) => (
              <SourcePill key={i} src={src} />
            ))}
          </div>
        )}
      </div>
      {isUser && (
        <div className="flex-shrink-0 w-7 h-7 rounded-full bg-[#2e2e2e] flex items-center justify-center text-xs font-bold text-gray-300 ml-2 mt-1">
          U
        </div>
      )}
    </div>
  )
}
