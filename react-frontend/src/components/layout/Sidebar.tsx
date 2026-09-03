import { BrainCircuit } from 'lucide-react'
import { DocumentUpload } from '@/components/documents/DocumentUpload'

export function Sidebar() {
  return (
    <aside className="w-60 flex-shrink-0 flex flex-col bg-[#111111] border-r border-[#2e2e2e]">
      <div className="p-4 border-b border-[#2e2e2e]">
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 rounded-lg bg-violet-600/20 border border-violet-600/40 flex items-center justify-center">
            <BrainCircuit size={15} className="text-violet-400" />
          </div>
          <div>
            <span className="font-semibold text-sm text-gray-100">AgentFrameworkRag</span>
            <p className="text-[10px] text-gray-600 leading-none mt-0.5">RAG-powered chat</p>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto pt-2">
        <DocumentUpload />
      </div>

      <div className="p-3 border-t border-[#2e2e2e]">
        <div className="text-[10px] text-gray-700 space-y-0.5">
          <p>.NET Aspire + Agent Framework</p>
          <p>React + Vite + Tailwind v4</p>
        </div>
      </div>
    </aside>
  )
}
